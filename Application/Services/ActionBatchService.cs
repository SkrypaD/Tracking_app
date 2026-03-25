// <copyright file="ActionBatchService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using CartridgeApp.Application.DTOs;
using CartridgeApp.Application.Interfaces;
using CartridgeApp.Domain.Entities;
using CartridgeApp.Domain.Enums;
using CartridgeApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CartridgeApp.Application.Services;

public class ActionService : IActionService
{
    private readonly AppDbContext db;

    public ActionService(AppDbContext db) => this.db = db;

    public async Task<IEnumerable<ActionDto>> GetAllAsync(
        Guid? cartridgeId = null, Guid? officeId = null,
        Guid? companyId = null, DateTime? from = null, DateTime? to = null)
    {
        var query = this.db.Actions
            .Include(a => a.Admin)
            .Include(a => a.Office)
            .AsQueryable();

        if (cartridgeId.HasValue)
        {
            query = query.Where(a => a.CartridgeId == cartridgeId);
        }

        if (officeId.HasValue)
        {
            query = query.Where(a => a.OfficeId == officeId);
        }

        if (companyId.HasValue)
        {
            query = query.Where(a => a.Office.CompanyId == companyId);
        }

        if (from.HasValue)
        {
            query = query.Where(a => a.CreatedAt >= from);
        }

        if (to.HasValue)
        {
            query = query.Where(a => a.CreatedAt <= to);
        }

        var actions = await query.OrderByDescending(a => a.CreatedAt).ToListAsync();
        return actions.Select(MapToDto);
    }

    public async Task<ActionDto> CreateAsync(CreateActionRequest request, Guid adminId)
    {
        var cartridge = await this.db.Cartridges
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
            CreatedAt = DateTime.UtcNow,
        };

        // Update cartridge printed count
        cartridge.PrintedCount = request.CurrentPrinted;

        this.db.Actions.Add(action);
        await this.db.SaveChangesAsync();

        await this.db.Entry(action).Reference(a => a.Admin).LoadAsync();
        await this.db.Entry(action).Reference(a => a.Office).LoadAsync();
        return MapToDto(action);
    }

    private static ActionDto MapToDto(CartridgeAction a) => new (
        a.Id, a.ActionType, a.ActionType.ToString(),
        a.CreatedAt, a.CurrentPrinted,
        a.CartridgeId, a.OfficeId, a.Office?.Name ?? string.Empty,
        a.PrinterId, a.AdminId, a.Admin?.Name ?? string.Empty,
        a.BatchId);
}

public class BatchService : IBatchService
{
    private readonly AppDbContext db;
    private readonly IActionService actionService;

    public BatchService(AppDbContext db, IActionService actionService)
    {
        this.db = db;
        this.actionService = actionService;
    }

    public async Task<IEnumerable<BatchDto>> GetAllAsync(Guid? companyId = null)
    {
        var query = this.db.Batches
            .Include(b => b.Admin)
            .Include(b => b.Actions)
            .AsQueryable();

        if (companyId.HasValue)
        {
            query = query.Where(b => b.Admin.CompanyId == companyId);
        }

        var batches = await query.OrderByDescending(b => b.CreatedAt).ToListAsync();
        return batches.Select(MapToDto);
    }

    public async Task<BatchDetailDto> GetByIdAsync(Guid id)
    {
        var batch = await this.GetBatchOrThrowAsync(id);
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
            SentAt = DateTime.UtcNow,
        };

        this.db.Batches.Add(batch);
        await this.db.SaveChangesAsync();

        // Create a Sent action for each cartridge
        foreach (var cartridgeId in request.CartridgeIds)
        {
            await this.actionService.CreateAsync(
                new CreateActionRequest(
                cartridgeId, ActionType.Sent, 0, batch.Id), adminId);
        }

        return await this.GetByIdAsync(batch.Id);
    }

    public async Task<BatchDetailDto> ReceiveAsync(Guid id, ReceiveBatchRequest request, Guid adminId)
    {
        var batch = await this.db.Batches.FindAsync(id)
            ?? throw new KeyNotFoundException("Batch not found.");

        if (batch.Status == BatchStatus.Received)
        {
            throw new InvalidOperationException("Batch already received.");
        }

        // Create Refilled action for each returned cartridge
        foreach (var cartridgeId in request.ReceivedCartridgeIds)
        {
            await this.actionService.CreateAsync(
                new CreateActionRequest(
                cartridgeId, ActionType.Refilled, 0, batch.Id), adminId);
        }

        batch.Status = BatchStatus.Received;
        batch.ReceivedAt = DateTime.UtcNow;
        await this.db.SaveChangesAsync();

        return await this.GetByIdAsync(batch.Id);
    }

    private async Task<Batch> GetBatchOrThrowAsync(Guid id) =>
        await this.db.Batches
            .Include(b => b.Admin)
            .Include(b => b.Actions).ThenInclude(a => a.Admin)
            .Include(b => b.Actions).ThenInclude(a => a.Office)
            .FirstOrDefaultAsync(b => b.Id == id)
        ?? throw new KeyNotFoundException("Batch not found.");

    private static BatchDto MapToDto(Batch b) => new (
        b.Id, b.Status, b.Status.ToString(),
        b.ServiceCompanyName, b.CreatedAt, b.SentAt, b.ReceivedAt,
        b.AdminId, b.Admin?.Name ?? string.Empty, b.Actions.Count);

    private static BatchDetailDto MapToDetailDto(Batch b) => new (
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
