using CartridgeApp.Application.DTOs;
using CartridgeApp.Application.Interfaces;
using CartridgeApp.Domain.Entities;
using CartridgeApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CartridgeApp.Application.Services;

/// <summary>
/// Provides business logic for managing <see cref="Cartridge"/> entities,
/// including CRUD operations, QR code generation, and QR-based lookup.
/// </summary>
/// <remarks>
/// <para><b>QR code lifecycle:</b></para>
/// QR codes are generated once at cartridge creation and never change.
/// The code encodes a URL pointing to the cartridge's detail page.
/// Scanning the code via <see cref="GetByQrCodeAsync"/> returns the cartridge
/// along with its last action, which the frontend uses to suggest the next state change.
///
/// <para><b>Dependency on IQrService:</b></para>
/// QR generation is delegated to <see cref="IQrService"/> so that the output
/// format (PNG, SVG, URL) can be changed without modifying this service.
/// </remarks>
public class CartridgeService : ICartridgeService
{
    private readonly AppDbContext _db;
    private readonly IQrService _qr;

    /// <summary>
    /// Initialises a new instance of <see cref="CartridgeService"/>.
    /// </summary>
    /// <param name="db">The EF Core database context.</param>
    /// <param name="qr">The QR code generation service.</param>
    public CartridgeService(AppDbContext db, IQrService qr)
    {
        _db = db;
        _qr = qr;
    }

    /// <summary>
    /// Returns a filtered list of cartridges with their last known state.
    /// </summary>
    /// <param name="printerId">
    /// Optional. When provided, only returns cartridges belonging to the specified printer.
    /// </param>
    /// <param name="companyId">
    /// Optional. When provided, only returns cartridges belonging to the specified company
    /// (resolved via <c>Cartridge → Printer → Office → Company</c>).
    /// </param>
    /// <returns>
    /// A collection of <see cref="CartridgeDto"/> objects ordered by insertion order.
    /// Each DTO includes the last action type and timestamp for state inference.
    /// </returns>
    public async Task<IEnumerable<CartridgeDto>> GetAllAsync(
        Guid? printerId = null, Guid? companyId = null)
    {
        var query = _db.Cartridges
            .Include(c => c.Printer).ThenInclude(p => p.Office)
            .Include(c => c.CartridgeType)
            .Include(c => c.Actions.OrderByDescending(a => a.CreatedAt).Take(1))
            .AsQueryable();

        if (printerId.HasValue)
            query = query.Where(c => c.PrinterId == printerId);

        if (companyId.HasValue)
            query = query.Where(c => c.Printer.Office.CompanyId == companyId);

        var cartridges = await query.ToListAsync();
        return cartridges.Select(MapToDto);
    }

    /// <summary>
    /// Returns a single cartridge by its primary key.
    /// </summary>
    /// <param name="id">The cartridge's unique identifier.</param>
    /// <returns>A <see cref="CartridgeDto"/> for the requested cartridge.</returns>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no cartridge with the given <paramref name="id"/> exists.
    /// </exception>
    public async Task<CartridgeDto> GetByIdAsync(Guid id)
    {
        var c = await GetCartridgeOrThrowAsync(id);
        return MapToDto(c);
    }

    /// <summary>
    /// Looks up a cartridge by the value encoded in its QR code.
    /// </summary>
    /// <param name="qrCode">
    /// The QR code string scanned from the physical cartridge label.
    /// This is the base64-encoded PNG stored in <see cref="Cartridge.QrCode"/>.
    /// </param>
    /// <returns>
    /// A <see cref="CartridgeDto"/> including the last action type,
    /// which the frontend uses to pre-suggest the next action.
    /// </returns>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when the scanned QR code does not match any known cartridge.
    /// </exception>
    /// <remarks>
    /// This is the primary entry point for the mobile scanning flow.
    /// The frontend calls this endpoint immediately after a successful QR scan.
    /// </remarks>
    public async Task<CartridgeDto> GetByQrCodeAsync(string qrCode)
    {
        var c = await _db.Cartridges
            .Include(c => c.Printer).ThenInclude(p => p.Office)
            .Include(c => c.CartridgeType)
            .Include(c => c.Actions.OrderByDescending(a => a.CreatedAt).Take(1))
            .FirstOrDefaultAsync(c => c.QrCode == qrCode)
            ?? throw new KeyNotFoundException("Cartridge not found.");

        return MapToDto(c);
    }

    /// <summary>
    /// Creates a new cartridge and generates its unique QR code.
    /// </summary>
    /// <param name="request">The creation payload containing printer, type, and optional description.</param>
    /// <returns>
    /// The newly created <see cref="CartridgeDto"/> including the generated <c>QrCode</c> field.
    /// </returns>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when the referenced <c>PrinterId</c> or <c>CartridgeTypeId</c> does not exist.
    /// </exception>
    /// <remarks>
    /// <b>QR generation order:</b>
    /// The cartridge is inserted first to obtain a stable <see cref="Cartridge.Id"/>,
    /// then the QR code is generated using that ID and saved in a second write.
    /// This ensures the QR code always encodes the correct, permanent identifier.
    /// </remarks>
    public async Task<CartridgeDto> CreateAsync(CreateCartridgeRequest request)
    {
        _ = await _db.Printers.FindAsync(request.PrinterId)
            ?? throw new KeyNotFoundException("Printer not found.");

        _ = await _db.CartridgeTypes.FindAsync(request.CartridgeTypeId)
            ?? throw new KeyNotFoundException("Cartridge type not found.");

        var cartridge = new Cartridge
        {
            Id = Guid.NewGuid(),
            Description = request.Description,
            PrinterId = request.PrinterId,
            CartridgeTypeId = request.CartridgeTypeId,
            PrintedCount = 0,
            QrCode = string.Empty
        };

        // Generate QR after ID is known so it encodes the correct URL
        cartridge.QrCode = _qr.Generate(cartridge.Id);

        _db.Cartridges.Add(cartridge);
        await _db.SaveChangesAsync();

        return await GetByIdAsync(cartridge.Id);
    }

    /// <summary>
    /// Updates the mutable fields of an existing cartridge.
    /// </summary>
    /// <param name="id">The unique identifier of the cartridge to update.</param>
    /// <param name="request">The fields to update (description and cartridge type).</param>
    /// <returns>The updated <see cref="CartridgeDto"/>.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the cartridge does not exist.</exception>
    /// <remarks>
    /// The <see cref="Cartridge.QrCode"/> and <see cref="Cartridge.PrinterId"/>
    /// are immutable after creation and cannot be changed via this method.
    /// </remarks>
    public async Task<CartridgeDto> UpdateAsync(Guid id, UpdateCartridgeRequest request)
    {
        var cartridge = await GetCartridgeOrThrowAsync(id);
        cartridge.Description = request.Description;
        cartridge.CartridgeTypeId = request.CartridgeTypeId;
        await _db.SaveChangesAsync();
        return MapToDto(cartridge);
    }

    /// <summary>
    /// Deletes a cartridge and all associated action records.
    /// </summary>
    /// <param name="id">The unique identifier of the cartridge to delete.</param>
    /// <exception cref="KeyNotFoundException">Thrown when the cartridge does not exist.</exception>
    /// <remarks>
    /// <b>Warning:</b> Deletion is permanent and removes the cartridge's entire action history.
    /// In most operational scenarios, cartridges should be depleted rather than deleted.
    /// Use this only to remove incorrectly created entries.
    /// </remarks>
    public async Task DeleteAsync(Guid id)
    {
        var cartridge = await _db.Cartridges.FindAsync(id)
            ?? throw new KeyNotFoundException("Cartridge not found.");
        _db.Cartridges.Remove(cartridge);
        await _db.SaveChangesAsync();
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private async Task<Cartridge> GetCartridgeOrThrowAsync(Guid id) =>
        await _db.Cartridges
            .Include(c => c.Printer).ThenInclude(p => p.Office)
            .Include(c => c.CartridgeType)
            .Include(c => c.Actions.OrderByDescending(a => a.CreatedAt).Take(1))
            .FirstOrDefaultAsync(c => c.Id == id)
        ?? throw new KeyNotFoundException("Cartridge not found.");

    /// <summary>
    /// Maps a <see cref="Cartridge"/> entity to its DTO representation.
    /// The last action is included to allow the frontend to infer the current state.
    /// </summary>
    private static CartridgeDto MapToDto(Cartridge c)
    {
        var lastAction = c.Actions.OrderByDescending(a => a.CreatedAt).FirstOrDefault();
        return new CartridgeDto(
            c.Id, c.Description, c.PrintedCount, c.QrCode,
            c.PrinterId, c.Printer.HostIp,
            c.CartridgeTypeId, c.CartridgeType.Name,
            lastAction?.ActionType, lastAction?.CreatedAt);
    }
}