// <copyright file="Dtos.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using CartridgeApp.Domain.Enums;

namespace CartridgeApp.Application.DTOs;

// ── Auth ─────────────────────────────────────────────────────────────────────
public record LoginRequest(string Email, string Password);
public record LoginResponse(string Token, string Name, string Role, Guid? CompanyId);

public record RegisterAdminRequest(
    string Name,
    string Email,
    string Password,
    UserRole Role,
    Guid? CompanyId);

// ── Company ───────────────────────────────────────────────────────────────────
public record CompanyDto(Guid Id, string Name);
public record CreateCompanyRequest(string Name);
public record UpdateCompanyRequest(string Name);

// ── Building ──────────────────────────────────────────────────────────────────
public record BuildingDto(Guid Id, string Name, string Address);
public record CreateBuildingRequest(string Name, string Address);
public record UpdateBuildingRequest(string Name, string Address);

// ── Office ────────────────────────────────────────────────────────────────────
public record OfficeDto(Guid Id, string Name, string? Description, Guid CompanyId, string CompanyName, Guid BuildingId, string BuildingName);
public record CreateOfficeRequest(string Name, string? Description, Guid CompanyId, Guid BuildingId);
public record UpdateOfficeRequest(string Name, string? Description);

// ── PrinterType ───────────────────────────────────────────────────────────────
public record PrinterTypeDto(Guid Id, string Name);
public record CreatePrinterTypeRequest(string Name);

// ── Printer ───────────────────────────────────────────────────────────────────
public record PrinterDto(
    Guid Id, string? Description, string HostIp,
    uint PrintedCount, uint Users, bool IsLocal,
    Guid OfficeId, string OfficeName,
    Guid PrinterTypeId, string PrinterTypeName);

public record CreatePrinterRequest(
    string HostIp, string? Description,
    uint Users, bool IsLocal,
    Guid OfficeId, Guid PrinterTypeId);

public record UpdatePrinterRequest(
    string HostIp, string? Description,
    uint Users, bool IsLocal);

// ── CartridgeType ─────────────────────────────────────────────────────────────
public record CartridgeTypeDto(Guid Id, string Name);
public record CreateCartridgeTypeRequest(string Name);

// ── Cartridge ─────────────────────────────────────────────────────────────────
public record CartridgeDto(
    Guid Id, string? Description,
    uint PrintedCount, string QrCode,
    Guid PrinterId, string PrinterHostIp,
    Guid CartridgeTypeId, string CartridgeTypeName,
    ActionType? LastActionType, DateTime? LastActionAt);

public record CreateCartridgeRequest(
    string? Description,
    Guid PrinterId,
    Guid CartridgeTypeId);

public record UpdateCartridgeRequest(string? Description, Guid CartridgeTypeId);

// ── Action ────────────────────────────────────────────────────────────────────
public record ActionDto(
    Guid Id, ActionType ActionType, string ActionTypeName,
    DateTime CreatedAt, uint CurrentPrinted,
    Guid CartridgeId, Guid OfficeId, string OfficeName,
    Guid PrinterId, Guid AdminId, string AdminName,
    Guid? BatchId);

public record CreateActionRequest(
    Guid CartridgeId,
    ActionType ActionType,
    uint CurrentPrinted,
    Guid? BatchId);

// ── Batch ─────────────────────────────────────────────────────────────────────
public record BatchDto(
    Guid Id, BatchStatus Status, string StatusName,
    string? ServiceCompanyName,
    DateTime CreatedAt, DateTime? SentAt, DateTime? ReceivedAt,
    Guid AdminId, string AdminName,
    int CartridgeCount);

public record BatchDetailDto(
    Guid Id, BatchStatus Status, string StatusName,
    string? ServiceCompanyName,
    DateTime CreatedAt, DateTime? SentAt, DateTime? ReceivedAt,
    Guid AdminId, string AdminName,
    IEnumerable<ActionDto> Actions);

public record CreateBatchRequest(
    string? ServiceCompanyName,
    IEnumerable<Guid> CartridgeIds);

public record ReceiveBatchRequest(
    IEnumerable<Guid> ReceivedCartridgeIds);

// ── Stats ─────────────────────────────────────────────────────────────────────
public record DashboardStatsDto(
    int TotalCartridges,
    int CartridgesSentForRefill,
    int CartridgesDepletedThisMonth,
    int CartridgesIssuedThisMonth,
    IEnumerable<CartridgeTypeStatDto> ByType,
    IEnumerable<OfficeStatDto> ByOffice);

public record CartridgeTypeStatDto(string TypeName, int RefillCount);
public record OfficeStatDto(string OfficeName, int ActionCount);