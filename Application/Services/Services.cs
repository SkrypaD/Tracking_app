// <copyright file="Services.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using CartridgeApp.Application.DTOs;
using CartridgeApp.Application.Interfaces;
using CartridgeApp.Domain.Entities;
using CartridgeApp.Domain.Enums;
using CartridgeApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using QRCoder;

namespace CartridgeApp.Application.Services;

public class CompanyService : ICompanyService
{
    private readonly AppDbContext db;

    public CompanyService(AppDbContext db) => this.db = db;

    public async Task<IEnumerable<CompanyDto>> GetAllAsync() =>
        (await this.db.Companies.ToListAsync()).Select(c => new CompanyDto(c.Id, c.Name));

    public async Task<CompanyDto> GetByIdAsync(Guid id)
    {
        var c = await this.db.Companies.FindAsync(id) ?? throw new KeyNotFoundException();
        return new CompanyDto(c.Id, c.Name);
    }

    public async Task<CompanyDto> CreateAsync(CreateCompanyRequest request)
    {
        var company = new Company { Id = Guid.NewGuid(), Name = request.Name };
        this.db.Companies.Add(company);
        await this.db.SaveChangesAsync();
        return new CompanyDto(company.Id, company.Name);
    }

    public async Task<CompanyDto> UpdateAsync(Guid id, UpdateCompanyRequest request)
    {
        var company = await this.db.Companies.FindAsync(id) ?? throw new KeyNotFoundException();
        company.Name = request.Name;
        await this.db.SaveChangesAsync();
        return new CompanyDto(company.Id, company.Name);
    }

    public async Task DeleteAsync(Guid id)
    {
        var company = await this.db.Companies.FindAsync(id) ?? throw new KeyNotFoundException();
        this.db.Companies.Remove(company);
        await this.db.SaveChangesAsync();
    }
}

public class BuildingService : IBuildingService
{
    private readonly AppDbContext db;

    public BuildingService(AppDbContext db) => this.db = db;

    public async Task<IEnumerable<BuildingDto>> GetAllAsync() =>
        (await this.db.Buildings.ToListAsync()).Select(b => new BuildingDto(b.Id, b.Name, b.Address));

    public async Task<BuildingDto> GetByIdAsync(Guid id)
    {
        var b = await this.db.Buildings.FindAsync(id) ?? throw new KeyNotFoundException();
        return new BuildingDto(b.Id, b.Name, b.Address);
    }

    public async Task<BuildingDto> CreateAsync(CreateBuildingRequest request)
    {
        var building = new Building { Id = Guid.NewGuid(), Name = request.Name, Address = request.Address };
        this.db.Buildings.Add(building);
        await this.db.SaveChangesAsync();
        return new BuildingDto(building.Id, building.Name, building.Address);
    }

    public async Task<BuildingDto> UpdateAsync(Guid id, UpdateBuildingRequest request)
    {
        var building = await this.db.Buildings.FindAsync(id) ?? throw new KeyNotFoundException();
        building.Name = request.Name;
        building.Address = request.Address;
        await this.db.SaveChangesAsync();
        return new BuildingDto(building.Id, building.Name, building.Address);
    }

    public async Task DeleteAsync(Guid id)
    {
        var building = await this.db.Buildings.FindAsync(id) ?? throw new KeyNotFoundException();
        this.db.Buildings.Remove(building);
        await this.db.SaveChangesAsync();
    }
}

public class OfficeService : IOfficeService
{
    private readonly AppDbContext db;

    public OfficeService(AppDbContext db) => this.db = db;

    public async Task<IEnumerable<OfficeDto>> GetAllAsync(Guid? companyId = null)
    {
        var query = this.db.Offices
            .Include(o => o.Company)
            .Include(o => o.Building)
            .AsQueryable();

        if (companyId.HasValue)
        {
            query = query.Where(o => o.CompanyId == companyId);
        }

        return (await query.ToListAsync()).Select(MapToDto);
    }

    public async Task<OfficeDto> GetByIdAsync(Guid id) =>
        MapToDto(await this.GetOrThrowAsync(id));

    public async Task<OfficeDto> CreateAsync(CreateOfficeRequest request)
    {
        var office = new Office
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            CompanyId = request.CompanyId,
            BuildingId = request.BuildingId,
        };
        this.db.Offices.Add(office);
        await this.db.SaveChangesAsync();
        return await this.GetByIdAsync(office.Id);
    }

    public async Task<OfficeDto> UpdateAsync(Guid id, UpdateOfficeRequest request)
    {
        var office = await this.GetOrThrowAsync(id);
        office.Name = request.Name;
        office.Description = request.Description;
        await this.db.SaveChangesAsync();
        return MapToDto(office);
    }

    public async Task DeleteAsync(Guid id)
    {
        var office = await this.db.Offices.FindAsync(id) ?? throw new KeyNotFoundException();
        this.db.Offices.Remove(office);
        await this.db.SaveChangesAsync();
    }

    private async Task<Office> GetOrThrowAsync(Guid id) =>
        await this.db.Offices.Include(o => o.Company).Include(o => o.Building)
            .FirstOrDefaultAsync(o => o.Id == id)
        ?? throw new KeyNotFoundException();

    private static OfficeDto MapToDto(Office o) => new (
        o.Id, o.Name, o.Description,
        o.CompanyId, o.Company.Name,
        o.BuildingId, o.Building.Name);
}

public class PrinterService : IPrinterService
{
    private readonly AppDbContext db;

    public PrinterService(AppDbContext db) => this.db = db;

    public async Task<IEnumerable<PrinterDto>> GetAllAsync(Guid? officeId = null)
    {
        var query = this.db.Printers
            .Include(p => p.Office)
            .Include(p => p.PrinterType)
            .AsQueryable();

        if (officeId.HasValue)
        {
            query = query.Where(p => p.OfficeId == officeId);
        }

        return (await query.ToListAsync()).Select(MapToDto);
    }

    public async Task<PrinterDto> GetByIdAsync(Guid id) => MapToDto(await this.GetOrThrowAsync(id));

    public async Task<PrinterDto> CreateAsync(CreatePrinterRequest request)
    {
        var printer = new Printer
        {
            Id = Guid.NewGuid(),
            HostIp = request.HostIp,
            Description = request.Description,
            Users = request.Users,
            IsLocal = request.IsLocal,
            OfficeId = request.OfficeId,
            PrinterTypeId = request.PrinterTypeId,
        };
        this.db.Printers.Add(printer);
        await this.db.SaveChangesAsync();
        return await this.GetByIdAsync(printer.Id);
    }

    public async Task<PrinterDto> UpdateAsync(Guid id, UpdatePrinterRequest request)
    {
        var printer = await this.GetOrThrowAsync(id);
        printer.HostIp = request.HostIp;
        printer.Description = request.Description;
        printer.Users = request.Users;
        printer.IsLocal = request.IsLocal;
        await this.db.SaveChangesAsync();
        return MapToDto(printer);
    }

    public async Task DeleteAsync(Guid id)
    {
        var printer = await this.db.Printers.FindAsync(id) ?? throw new KeyNotFoundException();
        this.db.Printers.Remove(printer);
        await this.db.SaveChangesAsync();
    }

    private async Task<Printer> GetOrThrowAsync(Guid id) =>
        await this.db.Printers.Include(p => p.Office).Include(p => p.PrinterType)
            .FirstOrDefaultAsync(p => p.Id == id)
        ?? throw new KeyNotFoundException();

    private static PrinterDto MapToDto(Printer p) => new (
        p.Id, p.Description, p.HostIp, p.PrintedCount, p.Users, p.IsLocal,
        p.OfficeId, p.Office.Name, p.PrinterTypeId, p.PrinterType.Name);
}

public class StatsService : IStatsService
{
    private readonly AppDbContext db;

    public StatsService(AppDbContext db) => this.db = db;

    public async Task<DashboardStatsDto> GetDashboardAsync(Guid? companyId = null)
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var cartridgeQuery = this.db.Cartridges
            .Include(c => c.Printer).ThenInclude(p => p.Office)
            .AsQueryable();

        if (companyId.HasValue)
        {
            cartridgeQuery = cartridgeQuery.Where(c => c.Printer.Office.CompanyId == companyId);
        }

        var totalCartridges = await cartridgeQuery.CountAsync();

        var actionQuery = this.db.Actions
            .Include(a => a.Office)
            .AsQueryable();

        if (companyId.HasValue)
        {
            actionQuery = actionQuery.Where(a => a.Office.CompanyId == companyId);
        }

        // Cartridges currently sent (last action = Sent)
        var sentCount = await this.db.Cartridges
            .Where(c => c.Actions
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => a.ActionType)
                .FirstOrDefault() == ActionType.Sent)
            .CountAsync();

        var depletedThisMonth = await actionQuery
            .Where(a => a.ActionType == ActionType.Depleted && a.CreatedAt >= monthStart)
            .CountAsync();

        var issuedThisMonth = await actionQuery
            .Where(a => a.ActionType == ActionType.Issued && a.CreatedAt >= monthStart)
            .CountAsync();

        // Top cartridge types by refill count
        var byType = await this.db.Actions
            .Include(a => a.Cartridge).ThenInclude(c => c.CartridgeType)
            .Where(a => a.ActionType == ActionType.Refilled)
            .GroupBy(a => a.Cartridge.CartridgeType.Name)
            .Select(g => new CartridgeTypeStatDto(g.Key, g.Count()))
            .OrderByDescending(x => x.RefillCount)
            .Take(10)
            .ToListAsync();

        // Actions per office
        var byOffice = await actionQuery
            .GroupBy(a => a.Office.Name)
            .Select(g => new OfficeStatDto(g.Key, g.Count()))
            .OrderByDescending(x => x.ActionCount)
            .Take(10)
            .ToListAsync();

        return new DashboardStatsDto(
            totalCartridges, sentCount,
            depletedThisMonth, issuedThisMonth,
            byType, byOffice);
    }
}

public class QrService : IQrService
{
    private readonly string baseUrl;

    public QrService(string baseUrl) => this.baseUrl = baseUrl;

    public string Generate(Guid cartridgeId)
    {
        var url = $"{this.baseUrl}/cartridges/{cartridgeId}";
        using var qrGenerator = new QRCodeGenerator();
        using var qrData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrData);
        var bytes = qrCode.GetGraphic(10);
        return Convert.ToBase64String(bytes); // return as base64 PNG
    }
}
