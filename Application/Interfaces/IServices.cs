using CartridgeApp.Application.DTOs;

namespace CartridgeApp.Application.Interfaces;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request);
    Task<AdminDto> RegisterAsync(RegisterAdminRequest request);
}

public interface ICompanyService
{
    Task<IEnumerable<CompanyDto>> GetAllAsync();
    Task<CompanyDto> GetByIdAsync(Guid id);
    Task<CompanyDto> CreateAsync(CreateCompanyRequest request);
    Task<CompanyDto> UpdateAsync(Guid id, UpdateCompanyRequest request);
    Task DeleteAsync(Guid id);
}

public interface IBuildingService
{
    Task<IEnumerable<BuildingDto>> GetAllAsync();
    Task<BuildingDto> GetByIdAsync(Guid id);
    Task<BuildingDto> CreateAsync(CreateBuildingRequest request);
    Task<BuildingDto> UpdateAsync(Guid id, UpdateBuildingRequest request);
    Task DeleteAsync(Guid id);
}

public interface IOfficeService
{
    Task<IEnumerable<OfficeDto>> GetAllAsync(Guid? companyId = null);
    Task<OfficeDto> GetByIdAsync(Guid id);
    Task<OfficeDto> CreateAsync(CreateOfficeRequest request);
    Task<OfficeDto> UpdateAsync(Guid id, UpdateOfficeRequest request);
    Task DeleteAsync(Guid id);
}

public interface IPrinterService
{
    Task<IEnumerable<PrinterDto>> GetAllAsync(Guid? officeId = null);
    Task<PrinterDto> GetByIdAsync(Guid id);
    Task<PrinterDto> CreateAsync(CreatePrinterRequest request);
    Task<PrinterDto> UpdateAsync(Guid id, UpdatePrinterRequest request);
    Task DeleteAsync(Guid id);
}

public interface ICartridgeService
{
    Task<IEnumerable<CartridgeDto>> GetAllAsync(Guid? printerId = null, Guid? companyId = null);
    Task<CartridgeDto> GetByIdAsync(Guid id);
    Task<CartridgeDto> GetByQrCodeAsync(string qrCode);
    Task<CartridgeDto> CreateAsync(CreateCartridgeRequest request);
    Task<CartridgeDto> UpdateAsync(Guid id, UpdateCartridgeRequest request);
    Task DeleteAsync(Guid id);
}

public interface IActionService
{
    Task<IEnumerable<ActionDto>> GetAllAsync(Guid? cartridgeId = null, Guid? officeId = null, Guid? companyId = null, DateTime? from = null, DateTime? to = null);
    Task<ActionDto> CreateAsync(CreateActionRequest request, Guid adminId);
}

public interface IBatchService
{
    Task<IEnumerable<BatchDto>> GetAllAsync(Guid? companyId = null);
    Task<BatchDetailDto> GetByIdAsync(Guid id);
    Task<BatchDetailDto> CreateAsync(CreateBatchRequest request, Guid adminId);
    Task<BatchDetailDto> ReceiveAsync(Guid id, ReceiveBatchRequest request, Guid adminId);
}

public interface IStatsService
{
    Task<DashboardStatsDto> GetDashboardAsync(Guid? companyId = null);
}

public interface IQrService
{
    string Generate(Guid cartridgeId);
}

// Dummy DTO needed in interface file
public record AdminDto(Guid Id, string Name, string Email, string Role, Guid? CompanyId);
