// <copyright file="Dtos.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using CartridgeApp.Domain.Enums;

namespace CartridgeApp.Application.DTOs;

// ── Auth ─────────────────────────────────────────────────────────────────────
public record LoginRequest(string email, string password);
public record LoginResponse(string token, string name, string role, Guid? companyId);

public record RegisterAdminRequest(
    string name,
    string email,
    string password,
    UserRole role,
    Guid? companyId);

// ── Company ───────────────────────────────────────────────────────────────────
public record CompanyDto(Guid id, string name);
public record CreateCompanyRequest(string name);
public record UpdateCompanyRequest(string name);

// ── Building ──────────────────────────────────────────────────────────────────
public record BuildingDto(Guid id, string name, string address);
public record CreateBuildingRequest(string name, string address);
public record UpdateBuildingRequest(string name, string address);

// ── Office ────────────────────────────────────────────────────────────────────
public record OfficeDto(Guid id, string name, string? description, Guid companyId, string companyName, Guid buildingId, string buildingName);
public record CreateOfficeRequest(string name, string? description, Guid companyId, Guid buildingId);
public record UpdateOfficeRequest(string name, string? description);

// ── PrinterType ───────────────────────────────────────────────────────────────
public record PrinterTypeDto(Guid id, string name);
public record CreatePrinterTypeRequest(string name);

// ── Printer ───────────────────────────────────────────────────────────────────
public record PrinterDto(
    Guid id, string? description, string hostIp,
    uint printedCount, uint users, bool isLocal,
    Guid officeId, string officeName,
    Guid printerTypeId, string printerTypeName);

public record CreatePrinterRequest(
    string hostIp, string? description,
    uint users, bool isLocal,
    Guid officeId, Guid printerTypeId);

public record UpdatePrinterRequest(
    string hostIp, string? description,
    uint users, bool isLocal);

// ── CartridgeType ─────────────────────────────────────────────────────────────
public record CartridgeTypeDto(Guid id, string name);
public record CreateCartridgeTypeRequest(string name);

// ── Cartridge ─────────────────────────────────────────────────────────────────
public record CartridgeDto(
    Guid id, string? description,
    uint printedCount, string qrCode,
    Guid printerId, string printerHostIp,
    Guid cartridgeTypeId, string cartridgeTypeName,
    ActionType? lastActionType, DateTime? lastActionAt);

public record CreateCartridgeRequest(
    string? description,
    Guid printerId,
    Guid cartridgeTypeId);

public record UpdateCartridgeRequest(string? description, Guid cartridgeTypeId);

// ── Action ────────────────────────────────────────────────────────────────────
public record ActionDto(
    Guid id, ActionType actionType, string actionTypeName,
    DateTime createdAt, uint currentPrinted,
    Guid cartridgeId, Guid officeId, string officeName,
    Guid printerId, Guid adminId, string adminName,
    Guid? batchId);

public record CreateActionRequest(
    Guid cartridgeId,
    ActionType actionType,
    uint currentPrinted,
    Guid? batchId);

// ── Batch ─────────────────────────────────────────────────────────────────────
public record BatchDto(
    Guid id, BatchStatus status, string statusName,
    string? serviceCompanyName,
    DateTime createdAt, DateTime? sentAt, DateTime? receivedAt,
    Guid adminId, string adminName,
    int cartridgeCount);

public record BatchDetailDto(
    Guid id, BatchStatus status, string statusName,
    string? serviceCompanyName,
    DateTime createdAt, DateTime? sentAt, DateTime? receivedAt,
    Guid adminId, string adminName,
    IEnumerable<ActionDto> actions);

public record CreateBatchRequest(
    string? serviceCompanyName,
    IEnumerable<Guid> cartridgeIds);  // cartridges to include

public record ReceiveBatchRequest(
    IEnumerable<Guid> receivedCartridgeIds);  // subset that came back

// ── Stats ─────────────────────────────────────────────────────────────────────
public record DashboardStatsDto(
    int totalCartridges,
    int cartridgesSentForRefill,
    int cartridgesDepletedThisMonth,
    int cartridgesIssuedThisMonth,
    IEnumerable<CartridgeTypeStatDto> byType,
    IEnumerable<OfficeStatDto> byOffice);

public record CartridgeTypeStatDto(string typeName, int refillCount);
public record OfficeStatDto(string officeName, int actionCount);
