using CartridgeApp.Domain.Enums;

namespace CartridgeApp.Domain.Entities;

public class Company
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<Office> Offices { get; set; } = new List<Office>();
    public ICollection<Admin> Admins { get; set; } = new List<Admin>();
}

public class Building
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;

    public ICollection<Office> Offices { get; set; } = new List<Office>();
}

public class Office
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public Guid BuildingId { get; set; }
    public Building Building { get; set; } = null!;

    public ICollection<Printer> Printers { get; set; } = new List<Printer>();
}

public class PrinterType
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<Printer> Printers { get; set; } = new List<Printer>();
}

public class Printer
{
    public Guid Id { get; set; }
    public string HostIp { get; set; } = string.Empty;
    public string? Description { get; set; }
    public uint PrintedCount { get; set; }
    public uint Users { get; set; }
    public bool IsLocal { get; set; }

    public Guid OfficeId { get; set; }
    public Office Office { get; set; } = null!;

    public Guid PrinterTypeId { get; set; }
    public PrinterType PrinterType { get; set; } = null!;

    public ICollection<Cartridge> Cartridges { get; set; } = new List<Cartridge>();
}

public class CartridgeType
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<Cartridge> Cartridges { get; set; } = new List<Cartridge>();
}

public class Cartridge
{
    public Guid Id { get; set; }
    public string? Description { get; set; }
    public uint PrintedCount { get; set; }
    public string QrCode { get; set; } = string.Empty; // base64 or URL

    public Guid PrinterId { get; set; }
    public Printer Printer { get; set; } = null!;

    public Guid CartridgeTypeId { get; set; }
    public CartridgeType CartridgeType { get; set; } = null!;

    public ICollection<CartridgeAction> Actions { get; set; } = new List<CartridgeAction>();
}

public class CartridgeAction
{
    public Guid Id { get; set; }
    public ActionType ActionType { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public uint CurrentPrinted { get; set; }

    public Guid CartridgeId { get; set; }
    public Cartridge Cartridge { get; set; } = null!;

    public Guid OfficeId { get; set; }
    public Office Office { get; set; } = null!;

    public Guid PrinterId { get; set; }
    public Printer Printer { get; set; } = null!;

    public Guid AdminId { get; set; }
    public Admin Admin { get; set; } = null!;

    // Nullable — only set for Sent/Refilled actions
    public Guid? BatchId { get; set; }
    public Batch? Batch { get; set; }
}

public class Batch
{
    public Guid Id { get; set; }
    public BatchStatus Status { get; set; } = BatchStatus.Pending;
    public string? ServiceCompanyName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
    public DateTime? ReceivedAt { get; set; }

    public Guid AdminId { get; set; }
    public Admin Admin { get; set; } = null!;

    public ICollection<CartridgeAction> Actions { get; set; } = new List<CartridgeAction>();
}

public class Admin
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Admin;

    // Null for SuperAdmin
    public Guid? CompanyId { get; set; }
    public Company? Company { get; set; }

    public ICollection<CartridgeAction> Actions { get; set; } = new List<CartridgeAction>();
    public ICollection<Batch> Batches { get; set; } = new List<Batch>();
}
