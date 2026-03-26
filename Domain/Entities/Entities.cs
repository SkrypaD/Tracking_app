using CartridgeApp.Domain.Enums;

namespace CartridgeApp.Domain.Entities;

/// <summary>
/// Represents a company that owns one or more offices in the system.
/// A company is the top-level tenant in the entity hierarchy:
/// <c>Company → Office → Printer → Cartridge → Action</c>.
/// </summary>
/// <remarks>
/// One company can have offices spread across multiple buildings.
/// Buildings are physical locations and are not owned by a company —
/// the <see cref="Office"/> entity holds both <c>CompanyId</c> and <c>BuildingId</c>.
/// </remarks>
public class Company
{
    /// <summary>Gets or sets the unique identifier of the company.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the display name of the company. Required, max 200 characters.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets the collection of offices that belong to this company.</summary>
    public ICollection<Office> Offices { get; set; } = new List<Office>();

    /// <summary>Gets the collection of admins assigned to this company.</summary>
    public ICollection<Admin> Admins { get; set; } = new List<Admin>();
}

/// <summary>
/// Represents a physical building that can host offices from multiple companies.
/// </summary>
/// <remarks>
/// A <see cref="Building"/> is a pure location entity — it has no ownership semantics.
/// Ownership is expressed through <see cref="Office.CompanyId"/>, not through the building.
/// This models real-world scenarios where a shared business centre contains offices
/// belonging to several different companies.
/// </remarks>
public class Building
{
    /// <summary>Gets or sets the unique identifier of the building.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the display name of the building.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the physical street address of the building.</summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>Gets the collection of offices physically located in this building.</summary>
    public ICollection<Office> Offices { get; set; } = new List<Office>();
}

/// <summary>
/// Represents a logical office unit that belongs to a <see cref="Company"/>
/// and is physically located inside a <see cref="Building"/>.
/// </summary>
/// <remarks>
/// An office acts as the primary organisational boundary for admins:
/// an <see cref="Admin"/> with role <c>Admin</c> can only see and manage
/// entities within their assigned company's offices.
/// <para>
/// The <see cref="CompanyId"/> is also denormalised onto <see cref="CartridgeAction"/>
/// (via <c>OfficeId</c>) to allow efficient filtering of actions by company
/// with a single JOIN rather than traversing the full entity chain.
/// </para>
/// </remarks>
public class Office
{
    /// <summary>Gets or sets the unique identifier of the office.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the display name of the office.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets an optional human-readable description of the office.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the ID of the company that owns this office.</summary>
    public Guid CompanyId { get; set; }

    /// <summary>Gets or sets the company that owns this office.</summary>
    public Company Company { get; set; } = null!;

    /// <summary>Gets or sets the ID of the building that physically contains this office.</summary>
    public Guid BuildingId { get; set; }

    /// <summary>Gets or sets the building that physically contains this office.</summary>
    public Building Building { get; set; } = null!;

    /// <summary>Gets the collection of printers located in this office.</summary>
    public ICollection<Printer> Printers { get; set; } = new List<Printer>();
}

/// <summary>
/// Represents a lookup table entry for printer model categories (e.g. "Laser", "Inkjet").
/// </summary>
public class PrinterType
{
    /// <summary>Gets or sets the unique identifier of the printer type.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the name of the printer type. Required, max 100 characters.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets the collection of printers of this type.</summary>
    public ICollection<Printer> Printers { get; set; } = new List<Printer>();
}

/// <summary>
/// Represents a physical printer device located in an <see cref="Office"/>.
/// </summary>
public class Printer
{
    /// <summary>Gets or sets the unique identifier of the printer.</summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the host IP address or hostname of the printer on the local network.
    /// Used to identify the device on the network; not required to be globally routable.
    /// </summary>
    public string HostIp { get; set; } = string.Empty;

    /// <summary>Gets or sets an optional description or label for the printer.</summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the cumulative page count printed by this device.
    /// Updated each time a <see cref="CartridgeAction"/> is logged for a cartridge in this printer.
    /// </summary>
    public uint PrintedCount { get; set; }

    /// <summary>Gets or sets the approximate number of users who use this printer.</summary>
    public uint Users { get; set; }

    /// <summary>
    /// Gets or sets whether the printer is a local (USB/direct) device
    /// as opposed to a networked device.
    /// </summary>
    public bool IsLocal { get; set; }

    /// <summary>Gets or sets the ID of the office where this printer is located.</summary>
    public Guid OfficeId { get; set; }

    /// <summary>Gets or sets the office where this printer is located.</summary>
    public Office Office { get; set; } = null!;

    /// <summary>Gets or sets the ID of the printer type.</summary>
    public Guid PrinterTypeId { get; set; }

    /// <summary>Gets or sets the type/model category of this printer.</summary>
    public PrinterType PrinterType { get; set; } = null!;

    /// <summary>Gets the collection of cartridges currently or previously installed in this printer.</summary>
    public ICollection<Cartridge> Cartridges { get; set; } = new List<Cartridge>();
}

/// <summary>
/// Represents a lookup table entry for cartridge model categories
/// (e.g. "Black Toner", "Color Ink").
/// </summary>
public class CartridgeType
{
    /// <summary>Gets or sets the unique identifier of the cartridge type.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the name of the cartridge type. Required, max 100 characters.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets the collection of cartridges of this type.</summary>
    public ICollection<Cartridge> Cartridges { get; set; } = new List<Cartridge>();
}

/// <summary>
/// Represents a physical cartridge unit tracked within the system.
/// </summary>
/// <remarks>
/// A cartridge is the primary entity that changes state over time.
/// Its lifecycle is recorded through a series of immutable <see cref="CartridgeAction"/> records.
/// The current state is inferred from the most recent action.
/// <para>
/// Each cartridge has a unique QR code generated at creation time.
/// Scanning the QR code via the mobile web interface retrieves the cartridge
/// and pre-suggests the next logical action based on the last recorded state.
/// </para>
/// <para>State transition diagram:</para>
/// <code>
///  [Created] → Issued → Depleted → Sent → Refilled → Issued → ...
///                ↑_______________________________↑
/// </code>
/// </remarks>
public class Cartridge
{
    /// <summary>Gets or sets the unique identifier of the cartridge.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets an optional human-readable description or label for this cartridge.</summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the cumulative page count printed by this cartridge.
    /// Updated each time a <see cref="CartridgeAction"/> is saved for this cartridge.
    /// </summary>
    public uint PrintedCount { get; set; }

    /// <summary>
    /// Gets or sets the base64-encoded PNG QR code image for this cartridge.
    /// Generated automatically at creation. The QR code encodes the cartridge's
    /// detail URL: <c>{AppBaseUrl}/cartridges/{Id}</c>.
    /// </summary>
    public string QrCode { get; set; } = string.Empty;

    /// <summary>Gets or sets the ID of the printer this cartridge belongs to.</summary>
    public Guid PrinterId { get; set; }

    /// <summary>Gets or sets the printer this cartridge belongs to.</summary>
    public Printer Printer { get; set; } = null!;

    /// <summary>Gets or sets the ID of the cartridge type.</summary>
    public Guid CartridgeTypeId { get; set; }

    /// <summary>Gets or sets the type/model of this cartridge.</summary>
    public CartridgeType CartridgeType { get; set; } = null!;

    /// <summary>
    /// Gets the ordered collection of actions (state changes) recorded for this cartridge.
    /// Actions are immutable — the current state is always the most recent action.
    /// </summary>
    public ICollection<CartridgeAction> Actions { get; set; } = new List<CartridgeAction>();
}

/// <summary>
/// Represents an immutable record of a state change event for a <see cref="Cartridge"/>.
/// </summary>
/// <remarks>
/// Actions are the audit log of the system. They are never updated or deleted —
/// corrections are made by creating a new action. This guarantees full traceability
/// of every cartridge's history.
/// <para>
/// The <see cref="OfficeId"/> is stored directly on the action (denormalised)
/// to avoid a 5-join chain when querying actions by company.
/// See architecture note in <see cref="Office"/> for details.
/// </para>
/// </remarks>
public class CartridgeAction
{
    /// <summary>Gets or sets the unique identifier of the action.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the type of action performed (Issued, Depleted, Sent, Refilled).</summary>
    public ActionType ActionType { get; set; }

    /// <summary>Gets or sets the UTC timestamp when this action was recorded.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the printed page count of the cartridge at the time this action was logged.
    /// Provides a snapshot for historical analysis of cartridge usage rates.
    /// </summary>
    public uint CurrentPrinted { get; set; }

    /// <summary>Gets or sets the ID of the cartridge this action applies to.</summary>
    public Guid CartridgeId { get; set; }

    /// <summary>Gets or sets the cartridge this action applies to.</summary>
    public Cartridge Cartridge { get; set; } = null!;

    /// <summary>
    /// Gets or sets the ID of the office where the action took place.
    /// Denormalised from <c>Cartridge.Printer.Office</c> for query performance.
    /// </summary>
    public Guid OfficeId { get; set; }

    /// <summary>Gets or sets the office where the action took place.</summary>
    public Office Office { get; set; } = null!;

    /// <summary>Gets or sets the ID of the printer the cartridge was in at the time of the action.</summary>
    public Guid PrinterId { get; set; }

    /// <summary>Gets or sets the printer the cartridge was in at the time of the action.</summary>
    public Printer Printer { get; set; } = null!;

    /// <summary>Gets or sets the ID of the admin who performed the action.</summary>
    public Guid AdminId { get; set; }

    /// <summary>Gets or sets the admin who performed the action.</summary>
    public Admin Admin { get; set; } = null!;

    /// <summary>
    /// Gets or sets the optional ID of the <see cref="Batch"/> this action belongs to.
    /// Only set for actions with type <see cref="ActionType.Sent"/> or <see cref="ActionType.Refilled"/>.
    /// <c>null</c> for <see cref="ActionType.Issued"/> and <see cref="ActionType.Depleted"/> actions.
    /// </summary>
    public Guid? BatchId { get; set; }

    /// <summary>Gets or sets the batch this action belongs to, if any.</summary>
    public Batch? Batch { get; set; }
}

/// <summary>
/// Represents a batch of cartridges sent together to a service company for refilling.
/// </summary>
/// <remarks>
/// The batch lifecycle follows these status transitions:
/// <code>
///   Pending → Sent → Received
/// </code>
/// When a batch is created, all included cartridges receive a <see cref="ActionType.Sent"/> action.
/// When a batch is received, the returned cartridges receive a <see cref="ActionType.Refilled"/> action.
/// Partial receipt is supported — some cartridges may remain in <c>Sent</c> state
/// if they were not returned in the current delivery.
/// </remarks>
public class Batch
{
    /// <summary>Gets or sets the unique identifier of the batch.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the current lifecycle status of this batch.</summary>
    public BatchStatus Status { get; set; } = BatchStatus.Pending;

    /// <summary>
    /// Gets or sets the optional name of the external service company
    /// this batch was sent to. For reference only — not a foreign key.
    /// </summary>
    public string? ServiceCompanyName { get; set; }

    /// <summary>Gets or sets the UTC timestamp when this batch was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Gets or sets the UTC timestamp when this batch was dispatched. Set on creation.</summary>
    public DateTime? SentAt { get; set; }

    /// <summary>Gets or sets the UTC timestamp when this batch was received back.</summary>
    public DateTime? ReceivedAt { get; set; }

    /// <summary>Gets or sets the ID of the admin who created and sent this batch.</summary>
    public Guid AdminId { get; set; }

    /// <summary>Gets or sets the admin who created and sent this batch.</summary>
    public Admin Admin { get; set; } = null!;

    /// <summary>Gets the collection of actions associated with this batch (Sent + Refilled).</summary>
    public ICollection<CartridgeAction> Actions { get; set; } = new List<CartridgeAction>();
}

/// <summary>
/// Represents a user account in the system.
/// </summary>
/// <remarks>
/// Two roles exist:
/// <list type="bullet">
///   <item><see cref="UserRole.Admin"/> — scoped to a single company via <see cref="CompanyId"/>.</item>
///   <item><see cref="UserRole.SuperAdmin"/> — platform-wide access; <see cref="CompanyId"/> is <c>null</c>.</item>
/// </list>
/// Passwords are stored as BCrypt hashes. Plain-text passwords are never persisted.
/// </remarks>
public class Admin
{
    /// <summary>Gets or sets the unique identifier of the admin.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the full display name of the admin.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the unique email address used for login. Max 200 characters.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the BCrypt hash of the admin's password.
    /// Never expose this field in API responses.
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Gets or sets the role that determines access scope.</summary>
    public UserRole Role { get; set; } = UserRole.Admin;

    /// <summary>
    /// Gets or sets the ID of the company this admin belongs to.
    /// <c>null</c> for <see cref="UserRole.SuperAdmin"/> accounts.
    /// </summary>
    public Guid? CompanyId { get; set; }

    /// <summary>Gets or sets the company this admin belongs to, if any.</summary>
    public Company? Company { get; set; }

    /// <summary>Gets the collection of actions logged by this admin.</summary>
    public ICollection<CartridgeAction> Actions { get; set; } = new List<CartridgeAction>();

    /// <summary>Gets the collection of batches created by this admin.</summary>
    public ICollection<Batch> Batches { get; set; } = new List<Batch>();
}