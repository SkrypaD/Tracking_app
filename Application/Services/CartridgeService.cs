// <copyright file="CartridgeService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using CartridgeApp.Application.DTOs;
using CartridgeApp.Application.Interfaces;
using CartridgeApp.Domain.Entities;
using CartridgeApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CartridgeApp.Application.Services;

public class CartridgeService : ICartridgeService
{
    private readonly AppDbContext db;
    private readonly IQrService qr;

    public CartridgeService(AppDbContext db, IQrService qr)
    {
        this.db = db;
        this.qr = qr;
    }

    public async Task<IEnumerable<CartridgeDto>> GetAllAsync(Guid? printerId = null, Guid? companyId = null)
    {
        var query = this.db.Cartridges
            .Include(c => c.Printer).ThenInclude(p => p.Office)
            .Include(c => c.CartridgeType)
            .Include(c => c.Actions.OrderByDescending(a => a.CreatedAt).Take(1))
            .AsQueryable();

        if (printerId.HasValue)
        {
            query = query.Where(c => c.PrinterId == printerId);
        }

        if (companyId.HasValue)
        {
            query = query.Where(c => c.Printer.Office.CompanyId == companyId);
        }

        var cartridges = await query.ToListAsync();
        return cartridges.Select(MapToDto);
    }

    public async Task<CartridgeDto> GetByIdAsync(Guid id)
    {
        var c = await this.GetCartridgeOrThrowAsync(id);
        return MapToDto(c);
    }

    public async Task<CartridgeDto> GetByQrCodeAsync(string qrCode)
    {
        var c = await this.db.Cartridges
            .Include(c => c.Printer).ThenInclude(p => p.Office)
            .Include(c => c.CartridgeType)
            .Include(c => c.Actions.OrderByDescending(a => a.CreatedAt).Take(1))
            .FirstOrDefaultAsync(c => c.QrCode == qrCode)
            ?? throw new KeyNotFoundException("Cartridge not found.");

        return MapToDto(c);
    }

    public async Task<CartridgeDto> CreateAsync(CreateCartridgeRequest request)
    {
        var printer = await this.db.Printers.FindAsync(request.PrinterId)
            ?? throw new KeyNotFoundException("Printer not found.");

        var cartridgeType = await this.db.CartridgeTypes.FindAsync(request.CartridgeTypeId)
            ?? throw new KeyNotFoundException("Cartridge type not found.");

        var cartridge = new Cartridge
        {
            Id = Guid.NewGuid(),
            Description = request.Description,
            PrinterId = request.PrinterId,
            CartridgeTypeId = request.CartridgeTypeId,
            PrintedCount = 0,
            QrCode = string.Empty, // temp, will be set after we have the ID
        };

        // Generate QR code using the new ID
        cartridge.QrCode = this.qr.Generate(cartridge.Id);

        this.db.Cartridges.Add(cartridge);
        await this.db.SaveChangesAsync();

        return await this.GetByIdAsync(cartridge.Id);
    }

    public async Task<CartridgeDto> UpdateAsync(Guid id, UpdateCartridgeRequest request)
    {
        var cartridge = await this.GetCartridgeOrThrowAsync(id);
        cartridge.Description = request.Description;
        cartridge.CartridgeTypeId = request.CartridgeTypeId;
        await this.db.SaveChangesAsync();
        return MapToDto(cartridge);
    }

    public async Task DeleteAsync(Guid id)
    {
        var cartridge = await this.db.Cartridges.FindAsync(id)
            ?? throw new KeyNotFoundException("Cartridge not found.");
        this.db.Cartridges.Remove(cartridge);
        await this.db.SaveChangesAsync();
    }

    private async Task<Cartridge> GetCartridgeOrThrowAsync(Guid id) =>
        await this.db.Cartridges
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
