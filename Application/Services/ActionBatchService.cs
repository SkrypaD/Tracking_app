using CartridgeApp.Application.DTOs;
using CartridgeApp.Application.Interfaces;
using CartridgeApp.Domain.Entities;
using CartridgeApp.Domain.Enums;
using CartridgeApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CartridgeApp.Application.Services;

/// <summary>
/// Provides business logic for managing <see cref="Batch"/> entities.
/// A batch represents a group of cartridges sent together to an external
/// service company for refilling.
/// </summary>
/// <remarks>
/// <para><b>Batch lifecycle:</b></para>
/// <code>
///   CreateAsync()   → Batch(Status=Sent)   + CartridgeAction(Sent)   per cartridge
///   ReceiveAsync()  → Batch(Status=Received) + CartridgeAction(Refilled) per returned cartridge
/// </code>
/// Partial receipt is supported — <see cref="ReceiveBatchRequest.ReceivedCartridgeIds"/>
/// may be a subset of the originally sent cartridges.
/// Unreturned cartridges retain their <c>Sent</c> state until a subsequent receive call.
/// </remarks>
public class BatchService : IBatchService
{
    private readonly AppDbContext _db;
    private readonly IActionService _actionService;

    /// <summary>
    /// Initialises a new instance of <see cref="BatchService"/>.
    /// </summary>
    /// <param name="db">The EF Core database context.</param>
    /// <param name="actionService">
    /// The action service used to create <see cref="CartridgeAction"/> records
    /// for each cartridge in the batch.
    /// </param>
    public BatchService(AppDbContext db, IActionService actionService)
    {
        _db = db;
        _actionService = actionService;
    }

    /// <summary>
    /// Returns a summary list of all batches, optionally filtered by company.
    /// </summary>
    /// <param name="companyId">
    /// Optional. When provided, only returns batches created by admins
    /// belonging to the specified company.
    /// </param>
    /// <returns>
    /// A collection of <see cref="BatchDto"/> objects ordered by creation date (newest first),
    /// each including a <c>CartridgeCount</c> for display purposes.
    /// </returns>
    public async Task<IEnumerable<BatchDto>> GetAllAsync(Guid? companyId = null)
    {
        var query = _db.Batches
            .Include(b => b.Admin)
            .Include(b => b.Actions)
            .AsQueryable();

        if (companyId.HasValue)
            query = query.Where(b => b.Admin.CompanyId == companyId);

        return (await query.OrderByDescending(b => b.CreatedAt).ToListAsync())
            .Select(MapToDto);
    }

    /// <summary>
    /// Returns the full detail of a batch, including all associated actions.
    /// </summary>
    /// <param name="id">The unique identifier of the batch.</param>
    /// <returns>A <see cref="BatchDetailDto"/> with the full action list.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the batch does not exist.</exception>
    public async Task<BatchDetailDto> GetByIdAsync(Guid id) =>
        MapToDetailDto(await GetBatchOrThrowAsync(id));

    /// <summary>
    /// Creates a new batch, marks it as sent, and records a
    /// <see cref="ActionType.Sent"/> action for each included cartridge.
    /// </summary>
    /// <param name="request">
    /// The creation payload containing an optional service company name
    /// and the list of cartridge IDs to include.
    /// </param>
    /// <param name="adminId">The ID of the admin performing the operation.</param>
    /// <returns>The newly created <see cref="BatchDetailDto"/>.</returns>
    /// <remarks>
    /// The batch is persisted first, then <see cref="IActionService.CreateAsync"/>
    /// is called for each cartridge. The <c>BatchId</c> is passed to each action
    /// so they can be grouped when querying batch history.
    /// </remarks>
    public async Task<BatchDetailDto> CreateAsync(CreateBatchRequest request, Guid adminId)
    {
        var batch = new Batch
        {
            Id = Guid.NewGuid(),
            AdminId = adminId,
            ServiceCompanyName = request.ServiceCompanyName,
            Status = BatchStatus.Sent,
            CreatedAt = DateTime.UtcNow,
            SentAt = DateTime.UtcNow
        };

        _db.Batches.Add(batch);
        await _db.SaveChangesAsync();

        foreach (var cartridgeId in request.CartridgeIds)
        {
            await _actionService.CreateAsync(
                new CreateActionRequest(cartridgeId, ActionType.Sent, 0, batch.Id),
                adminId);
        }

        return await GetByIdAsync(batch.Id);
    }

    /// <summary>
    /// Marks a batch as received and records a <see cref="ActionType.Refilled"/>
    /// action for each cartridge that was returned.
    /// </summary>
    /// <param name="id">The unique identifier of the batch to receive.</param>
    /// <param name="request">
    /// Contains the IDs of the cartridges that were physically returned.
    /// May be a subset of all cartridges in the batch (partial receipt).
    /// </param>
    /// <param name="adminId">The ID of the admin performing the operation.</param>
    /// <returns>The updated <see cref="BatchDetailDto"/>.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the batch does not exist.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the batch has already been marked as received.
    /// </exception>
    /// <remarks>
    /// <b>Partial receipt:</b> If not all cartridges are returned at once,
    /// call this method again later with the remaining IDs.
    /// The batch status will be set to <c>Received</c> on the first call regardless —
    /// the status reflects the most recent receive event, not full completion.
    /// </remarks>
    public async Task<BatchDetailDto> ReceiveAsync(
        Guid id, ReceiveBatchRequest request, Guid adminId)
    {
        var batch = await _db.Batches.FindAsync(id)
            ?? throw new KeyNotFoundException("Batch not found.");

        if (batch.Status == BatchStatus.Received)
            throw new InvalidOperationException("Batch is already marked as received.");

        foreach (var cartridgeId in request.ReceivedCartridgeIds)
        {
            await _actionService.CreateAsync(
                new CreateActionRequest(cartridgeId, ActionType.Refilled, 0, batch.Id),
                adminId);
        }

        batch.Status = BatchStatus.Received;
        batch.ReceivedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return await GetByIdAsync(batch.Id);
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private async Task<Batch> GetBatchOrThrowAsync(Guid id) =>
        await _db.Batches
            .Include(b => b.Admin)
            .Include(b => b.Actions).ThenInclude(a => a.Admin)
            .Include(b => b.Actions).ThenInclude(a => a.Office)
            .FirstOrDefaultAsync(b => b.Id == id)
        ?? throw new KeyNotFoundException("Batch not found.");

    private static BatchDto MapToDto(Batch b) => new(
        b.Id, b.Status, b.Status.ToString(),
        b.ServiceCompanyName, b.CreatedAt, b.SentAt, b.ReceivedAt,
        b.AdminId, b.Admin?.Name ?? string.Empty, b.Actions.Count);

    private static BatchDetailDto MapToDetailDto(Batch b) => new(
        b.Id, b.Status, b.Status.ToString(),
        b.ServiceCompanyName, b.CreatedAt, b.SentAt, b.ReceivedAt,
        b.AdminId, b.Admin?.Name ?? string.Empty,
        b.Actions.Select(a => new ActionDto(
            a.Id, a.ActionType, a.ActionType.ToString(),
            a.CreatedAt, a.CurrentPrinted,
            a.CartridgeId, a.OfficeId, a.Office?.Name ?? string.Empty,
            a.PrinterId, a.AdminId, a.Admin?.Name ?? string.Empty,
            a.BatchId)));
}