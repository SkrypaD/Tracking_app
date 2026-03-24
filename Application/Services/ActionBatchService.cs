using CartridgeApp.Application.DTOs;
using CartridgeApp.Application.Interfaces;
using CartridgeApp.Domain.Entities;
using CartridgeApp.Domain.Enums;
using CartridgeApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CartridgeApp.Application.Services;

public class ActionService : IActionService
{
    private readonly AppDbContext _db;

    public ActionService(AppDbContext db) => _db = db;

    public async Task<IEnumerable<ActionDto>> GetAllAsync(
        Guid? cartridgeId = null, Guid? officeId = null,
        Guid? companyId = null, DateTime? from = null, DateTime? to = null)
    {
        var query = _db.Actions
            .Include(a => a.Admin)
            .Include(a => a.Office)
            .AsQueryable();

        if (cartridgeId.HasValue) query = query.Where(a => a.CartridgeId == cartridgeId);
        if (officeId.HasValue)    query = query.Where(a => a.OfficeId == officeId);
        if (companyId.HasValue)   query = query.Where(a => a.Office.CompanyId == companyId);
        if (from.HasValue)        query = query.Where(a => a.CreatedAt >= from);
        if (to.HasValue)          query = query.Where(a => a.CreatedAt <= to);

        var actions = await query.OrderByDescending(a => a.CreatedAt).ToListAsync();
        return actions.Select(MapToDto);
    }

    public async Task<ActionDto> CreateAsync(CreateActionRequest request, Guid adminId)
    {
        var cartridge = await _db.Cartridges
            .Include(c => c.Printer)
            .FirstOrDefaultAsync(c => c.Id == request.CartridgeId)
            ?? throw new KeyNotFoundException("Cartridge not found.");

        var action = new CartridgeAction
        {
            Id = Guid.NewGuid(),
            CartridgeId = request.CartridgeId,
            ActionType = request.ActionType,
            CurrentPrinted = request.CurrentPrinted,
            OfficeId = cartridge.Printer.OfficeId,
            PrinterId = cartridge.PrinterId,
            AdminId = adminId,
            BatchId = request.BatchId,
            CreatedAt = DateTime.UtcNow
        };

        // Update cartridge printed count
        cartridge.PrintedCount = request.CurrentPrinted;

        _db.Actions.Add(action);
        await _db.SaveChangesAsync();

        await _db.Entry(action).Reference(a => a.Admin).LoadAsync();
        await _db.Entry(action).Reference(a => a.Office).LoadAsync();
        return MapToDto(action);
    }

    private static ActionDto MapToDto(CartridgeAction a) => new(
        a.Id, a.ActionType, a.ActionType.ToString(),
        a.CreatedAt, a.CurrentPrinted,
        a.CartridgeId, a.OfficeId, a.Office?.Name ?? "",
        a.PrinterId, a.AdminId, a.Admin?.Name ?? "",
        a.BatchId);
}

public class BatchService : IBatchService
{
    private readonly AppDbContext _db;
    private readonly IActionService _actionService;

    public BatchService(AppDbContext db, IActionService actionService)
    {
        _db = db;
        _actionService = actionService;
    }

    public async Task<IEnumerable<BatchDto>> GetAllAsync(Guid? companyId = null)
    {
        var query = _db.Batches
            .Include(b => b.Admin)
            .Include(b => b.Actions)
            .AsQueryable();

        if (companyId.HasValue)
            query = query.Where(b => b.Admin.CompanyId == companyId);

        var batches = await query.OrderByDescending(b => b.CreatedAt).ToListAsync();
        return batches.Select(MapToDto);
    }

    public async Task<BatchDetailDto> GetByIdAsync(Guid id)
    {
        var batch = await GetBatchOrThrowAsync(id);
        return MapToDetailDto(batch);
    }

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

        // Create a Sent action for each cartridge
        foreach (var cartridgeId in request.CartridgeIds)
        {
            await _actionService.CreateAsync(new CreateActionRequest(
                cartridgeId, ActionType.Sent, 0, batch.Id), adminId);
        }

        return await GetByIdAsync(batch.Id);
    }

    public async Task<BatchDetailDto> ReceiveAsync(Guid id, ReceiveBatchRequest request, Guid adminId)
    {
        var batch = await _db.Batches.FindAsync(id)
            ?? throw new KeyNotFoundException("Batch not found.");

        if (batch.Status == BatchStatus.Received)
            throw new InvalidOperationException("Batch already received.");

        // Create Refilled action for each returned cartridge
        foreach (var cartridgeId in request.ReceivedCartridgeIds)
        {
            await _actionService.CreateAsync(new CreateActionRequest(
                cartridgeId, ActionType.Refilled, 0, batch.Id), adminId);
        }

        batch.Status = BatchStatus.Received;
        batch.ReceivedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return await GetByIdAsync(batch.Id);
    }

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
        b.AdminId, b.Admin?.Name ?? "", b.Actions.Count);

    private static BatchDetailDto MapToDetailDto(Batch b) => new(
        b.Id, b.Status, b.Status.ToString(),
        b.ServiceCompanyName, b.CreatedAt, b.SentAt, b.ReceivedAt,
        b.AdminId, b.Admin?.Name ?? "",
        b.Actions.Select(a => new ActionDto(
            a.Id, a.ActionType, a.ActionType.ToString(),
            a.CreatedAt, a.CurrentPrinted,
            a.CartridgeId, a.OfficeId, a.Office?.Name ?? "",
            a.PrinterId, a.AdminId, a.Admin?.Name ?? "",
            a.BatchId)));
}
