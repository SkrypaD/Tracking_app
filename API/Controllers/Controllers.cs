// <copyright file="Controllers.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Security.Claims;
using CartridgeApp.Application.DTOs;
using CartridgeApp.Application.Interfaces;
using CartridgeApp.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CartridgeApp.API.Controllers;

// ── Auth ──────────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService auth;

    public AuthController(IAuthService auth) => this.auth = auth;

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var result = await this.auth.LoginAsync(request);
        return this.Ok(result);
    }

    [HttpPost("register")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Register(RegisterAdminRequest request)
    {
        var result = await this.auth.RegisterAsync(request);
        return this.Created(string.Empty, result);
    }
}

// ── Company ───────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/companies")]
[Authorize]
public class CompaniesController : ControllerBase
{
    private readonly ICompanyService service;

    public CompaniesController(ICompanyService service) => this.service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll() => this.Ok(await this.service.GetAllAsync());

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id) => this.Ok(await this.service.GetByIdAsync(id));

    [HttpPost]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Create(CreateCompanyRequest request)
    {
        var result = await this.service.CreateAsync(request);
        return this.Created($"api/companies/{result.Id}", result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Update(Guid id, UpdateCompanyRequest request) =>
        this.Ok(await this.service.UpdateAsync(id, request));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await this.service.DeleteAsync(id);
        return this.NoContent();
    }
}

// ── Buildings ─────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/buildings")]
[Authorize]
public class BuildingsController : ControllerBase
{
    private readonly IBuildingService service;

    public BuildingsController(IBuildingService service) => this.service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll() => this.Ok(await this.service.GetAllAsync());

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id) => this.Ok(await this.service.GetByIdAsync(id));

    [HttpPost]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Create(CreateBuildingRequest request) =>
        this.Created(string.Empty, await this.service.CreateAsync(request));

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Update(Guid id, UpdateBuildingRequest request) =>
        this.Ok(await this.service.UpdateAsync(id, request));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await this.service.DeleteAsync(id);
        return this.NoContent();
    }
}

// ── Offices ───────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/offices")]
[Authorize]
public class OfficesController : ControllerBase
{
    private readonly IOfficeService service;

    public OfficesController(IOfficeService service) => this.service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? companyId) =>
        this.Ok(await this.service.GetAllAsync(companyId));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id) => this.Ok(await this.service.GetByIdAsync(id));

    [HttpPost]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Create(CreateOfficeRequest request) =>
        this.Created(string.Empty, await this.service.CreateAsync(request));

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Update(Guid id, UpdateOfficeRequest request) =>
        this.Ok(await this.service.UpdateAsync(id, request));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await this.service.DeleteAsync(id);
        return this.NoContent();
    }
}

// ── Printers ──────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/printers")]
[Authorize]
public class PrintersController : ControllerBase
{
    private readonly IPrinterService service;

    public PrintersController(IPrinterService service) => this.service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? officeId) =>
        this.Ok(await this.service.GetAllAsync(officeId));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id) => this.Ok(await this.service.GetByIdAsync(id));

    [HttpPost]
    public async Task<IActionResult> Create(CreatePrinterRequest request) =>
        this.Created(string.Empty, await this.service.CreateAsync(request));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdatePrinterRequest request) =>
        this.Ok(await this.service.UpdateAsync(id, request));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await this.service.DeleteAsync(id);
        return this.NoContent();
    }
}

// ── Cartridges ────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/cartridges")]
[Authorize]
public class CartridgesController : ControllerBase
{
    private readonly ICartridgeService service;

    public CartridgesController(ICartridgeService service) => this.service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? printerId, [FromQuery] Guid? companyId) =>
        this.Ok(await this.service.GetAllAsync(printerId, companyId));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id) => this.Ok(await this.service.GetByIdAsync(id));

    // Used after QR scan — find cartridge by its QR code value
    [HttpGet("scan/{qrCode}")]
    public async Task<IActionResult> GetByQr(string qrCode) => this.Ok(await this.service.GetByQrCodeAsync(qrCode));

    [HttpPost]
    public async Task<IActionResult> Create(CreateCartridgeRequest request) =>
        this.Created(string.Empty, await this.service.CreateAsync(request));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateCartridgeRequest request) =>
        this.Ok(await this.service.UpdateAsync(id, request));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await this.service.DeleteAsync(id);
        return this.NoContent();
    }
}

// ── Actions ───────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/actions")]
[Authorize]
public class ActionsController : ControllerBase
{
    private readonly IActionService service;

    public ActionsController(IActionService service) => this.service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? cartridgeId, [FromQuery] Guid? officeId,
        [FromQuery] Guid? companyId, [FromQuery] DateTime? from, [FromQuery] DateTime? to) =>
        this.Ok(await this.service.GetAllAsync(cartridgeId, officeId, companyId, from, to));

    [HttpPost]
    public async Task<IActionResult> Create(CreateActionRequest request)
    {
        var adminId = Guid.Parse(this.User.FindFirstValue(ClaimTypes.NameIdentifier) !);
        return this.Created(string.Empty, await this.service.CreateAsync(request, adminId));
    }
}

// ── Batches ───────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/batches")]
[Authorize]
public class BatchesController : ControllerBase
{
    private readonly IBatchService service;

    public BatchesController(IBatchService service) => this.service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? companyId) =>
        this.Ok(await this.service.GetAllAsync(companyId));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id) => this.Ok(await this.service.GetByIdAsync(id));

    [HttpPost]
    public async Task<IActionResult> Create(CreateBatchRequest request)
    {
        var adminId = Guid.Parse(this.User.FindFirstValue(ClaimTypes.NameIdentifier) !);
        return this.Created(string.Empty, await this.service.CreateAsync(request, adminId));
    }

    // Mark batch as received and log refilled actions for returned cartridges
    [HttpPost("{id:guid}/receive")]
    public async Task<IActionResult> Receive(Guid id, ReceiveBatchRequest request)
    {
        var adminId = Guid.Parse(this.User.FindFirstValue(ClaimTypes.NameIdentifier) !);
        return this.Ok(await this.service.ReceiveAsync(id, request, adminId));
    }
}

// ── Stats ─────────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/stats")]
[Authorize]
public class StatsController : ControllerBase
{
    private readonly IStatsService service;

    public StatsController(IStatsService service) => this.service = service;

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard([FromQuery] Guid? companyId) =>
        this.Ok(await this.service.GetDashboardAsync(companyId));
}
