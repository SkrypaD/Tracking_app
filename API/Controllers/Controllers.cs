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
    private readonly IAuthService _auth;
    public AuthController(IAuthService auth) => _auth = auth;

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var result = await _auth.LoginAsync(request);
        return Ok(result);
    }

    [HttpPost("register")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Register(RegisterAdminRequest request)
    {
        var result = await _auth.RegisterAsync(request);
        return Created("", result);
    }
}

// ── Company ───────────────────────────────────────────────────────────────────

[ApiController]
[Route("api/companies")]
[Authorize]
public class CompaniesController : ControllerBase
{
    private readonly ICompanyService _service;
    public CompaniesController(ICompanyService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _service.GetAllAsync());

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id) => Ok(await _service.GetByIdAsync(id));

    [HttpPost]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Create(CreateCompanyRequest request)
    {
        var result = await _service.CreateAsync(request);
        return Created($"api/companies/{result.Id}", result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Update(Guid id, UpdateCompanyRequest request) =>
        Ok(await _service.UpdateAsync(id, request));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }
}

// ── Buildings ─────────────────────────────────────────────────────────────────

[ApiController]
[Route("api/buildings")]
[Authorize]
public class BuildingsController : ControllerBase
{
    private readonly IBuildingService _service;
    public BuildingsController(IBuildingService service) => _service = service;

    [HttpGet] public async Task<IActionResult> GetAll() => Ok(await _service.GetAllAsync());
    [HttpGet("{id:guid}")] public async Task<IActionResult> GetById(Guid id) => Ok(await _service.GetByIdAsync(id));

    [HttpPost]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Create(CreateBuildingRequest request) =>
        Created("", await _service.CreateAsync(request));

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Update(Guid id, UpdateBuildingRequest request) =>
        Ok(await _service.UpdateAsync(id, request));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Delete(Guid id) { await _service.DeleteAsync(id); return NoContent(); }
}

// ── Offices ───────────────────────────────────────────────────────────────────

[ApiController]
[Route("api/offices")]
[Authorize]
public class OfficesController : ControllerBase
{
    private readonly IOfficeService _service;
    public OfficesController(IOfficeService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? companyId) =>
        Ok(await _service.GetAllAsync(companyId));

    [HttpGet("{id:guid}")] public async Task<IActionResult> GetById(Guid id) => Ok(await _service.GetByIdAsync(id));

    [HttpPost]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Create(CreateOfficeRequest request) =>
        Created("", await _service.CreateAsync(request));

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Update(Guid id, UpdateOfficeRequest request) =>
        Ok(await _service.UpdateAsync(id, request));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Delete(Guid id) { await _service.DeleteAsync(id); return NoContent(); }
}

// ── Printers ──────────────────────────────────────────────────────────────────

[ApiController]
[Route("api/printers")]
[Authorize]
public class PrintersController : ControllerBase
{
    private readonly IPrinterService _service;
    public PrintersController(IPrinterService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? officeId) =>
        Ok(await _service.GetAllAsync(officeId));

    [HttpGet("{id:guid}")] public async Task<IActionResult> GetById(Guid id) => Ok(await _service.GetByIdAsync(id));

    [HttpPost] public async Task<IActionResult> Create(CreatePrinterRequest request) =>
        Created("", await _service.CreateAsync(request));

    [HttpPut("{id:guid}")] public async Task<IActionResult> Update(Guid id, UpdatePrinterRequest request) =>
        Ok(await _service.UpdateAsync(id, request));

    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id)
    { await _service.DeleteAsync(id); return NoContent(); }
}

// ── Cartridges ────────────────────────────────────────────────────────────────

[ApiController]
[Route("api/cartridges")]
[Authorize]
public class CartridgesController : ControllerBase
{
    private readonly ICartridgeService _service;
    public CartridgesController(ICartridgeService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? printerId, [FromQuery] Guid? companyId) =>
        Ok(await _service.GetAllAsync(printerId, companyId));

    [HttpGet("{id:guid}")] public async Task<IActionResult> GetById(Guid id) => Ok(await _service.GetByIdAsync(id));

    // Used after QR scan — find cartridge by its QR code value
    [HttpGet("scan/{qrCode}")]
    public async Task<IActionResult> GetByQr(string qrCode) => Ok(await _service.GetByQrCodeAsync(qrCode));

    [HttpPost] public async Task<IActionResult> Create(CreateCartridgeRequest request) =>
        Created("", await _service.CreateAsync(request));

    [HttpPut("{id:guid}")] public async Task<IActionResult> Update(Guid id, UpdateCartridgeRequest request) =>
        Ok(await _service.UpdateAsync(id, request));

    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id)
    { await _service.DeleteAsync(id); return NoContent(); }
}

// ── Actions ───────────────────────────────────────────────────────────────────

[ApiController]
[Route("api/actions")]
[Authorize]
public class ActionsController : ControllerBase
{
    private readonly IActionService _service;
    public ActionsController(IActionService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? cartridgeId, [FromQuery] Guid? officeId,
        [FromQuery] Guid? companyId, [FromQuery] DateTime? from, [FromQuery] DateTime? to) =>
        Ok(await _service.GetAllAsync(cartridgeId, officeId, companyId, from, to));

    [HttpPost]
    public async Task<IActionResult> Create(CreateActionRequest request)
    {
        var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return Created("", await _service.CreateAsync(request, adminId));
    }
}

// ── Batches ───────────────────────────────────────────────────────────────────

[ApiController]
[Route("api/batches")]
[Authorize]
public class BatchesController : ControllerBase
{
    private readonly IBatchService _service;
    public BatchesController(IBatchService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? companyId) =>
        Ok(await _service.GetAllAsync(companyId));

    [HttpGet("{id:guid}")] public async Task<IActionResult> GetById(Guid id) => Ok(await _service.GetByIdAsync(id));

    [HttpPost]
    public async Task<IActionResult> Create(CreateBatchRequest request)
    {
        var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return Created("", await _service.CreateAsync(request, adminId));
    }

    // Mark batch as received and log refilled actions for returned cartridges
    [HttpPost("{id:guid}/receive")]
    public async Task<IActionResult> Receive(Guid id, ReceiveBatchRequest request)
    {
        var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return Ok(await _service.ReceiveAsync(id, request, adminId));
    }
}

// ── Stats ─────────────────────────────────────────────────────────────────────

[ApiController]
[Route("api/stats")]
[Authorize]
public class StatsController : ControllerBase
{
    private readonly IStatsService _service;
    public StatsController(IStatsService service) => _service = service;

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard([FromQuery] Guid? companyId) =>
        Ok(await _service.GetDashboardAsync(companyId));
}
