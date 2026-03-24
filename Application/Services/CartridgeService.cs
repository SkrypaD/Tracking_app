using CartridgeApp.Application.DTOs;
using CartridgeApp.Application.Interfaces;
using CartridgeApp.Domain.Entities;
using CartridgeApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CartridgeApp.Application.Services;

public class CartridgeService : ICartridgeService
{
    private readonly AppDbContext _db;
    private readonly IQrService _qr;

    public CartridgeService(AppDbContext db, IQrService qr)
    {
        _db = db;
        _qr = qr;
    }

    public async Task<IEnumerable<CartridgeDto>> GetAllAsync(Guid? printerId = null, Guid? companyId = null)
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

    public async Task<CartridgeDto> GetByIdAsync(Guid id)
    {
        var c = await GetCartridgeOrThrowAsync(id);
        return MapToDto(c);
    }

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

    public async Task<CartridgeDto> CreateAsync(CreateCartridgeRequest request)
    {
        var printer = await _db.Printers.FindAsync(request.PrinterId)
            ?? throw new KeyNotFoundException("Printer not found.");

        var cartridgeType = await _db.CartridgeTypes.FindAsync(request.CartridgeTypeId)
            ?? throw new KeyNotFoundException("Cartridge type not found.");

        var cartridge = new Cartridge
        {
            Id = Guid.NewGuid(),
            Description = request.Description,
            PrinterId = request.PrinterId,
            CartridgeTypeId = request.CartridgeTypeId,
            PrintedCount = 0,
            QrCode = "" // temp, will be set after we have the ID
        };

        // Generate QR code using the new ID
        cartridge.QrCode = _qr.Generate(cartridge.Id);

        _db.Cartridges.Add(cartridge);
        await _db.SaveChangesAsync();

        return await GetByIdAsync(cartridge.Id);
    }

    public async Task<CartridgeDto> UpdateAsync(Guid id, UpdateCartridgeRequest request)
    {
        var cartridge = await GetCartridgeOrThrowAsync(id);
        cartridge.Description = request.Description;
        cartridge.CartridgeTypeId = request.CartridgeTypeId;
        await _db.SaveChangesAsync();
        return MapToDto(cartridge);
    }

    public async Task DeleteAsync(Guid id)
    {
        var cartridge = await _db.Cartridges.FindAsync(id)
            ?? throw new KeyNotFoundException("Cartridge not found.");
        _db.Cartridges.Remove(cartridge);
        await _db.SaveChangesAsync();
    }

    private async Task<Cartridge> GetCartridgeOrThrowAsync(Guid id) =>
        await _db.Cartridges
            .Include(c => c.Printer).ThenInclude(p => p.Office)
            .Include(c => c.CartridgeType)
            .Include(c => c.Actions.OrderByDescending(a => a.CreatedAt).Take(1))
            .FirstOrDefaultAsync(c => c.Id == id)
        ?? throw new KeyNotFoundException("Cartridge not found.");

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
