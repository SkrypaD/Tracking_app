using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CartridgeApp.Application.DTOs;
using CartridgeApp.Application.Interfaces;
using CartridgeApp.Domain.Entities;
using CartridgeApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CartridgeApp.Application.Services;

/// <summary>
/// Handles authentication and admin registration for the Cartridge Tracker system.
/// </summary>
/// <remarks>
/// <para><b>Authentication flow:</b></para>
/// <code>
///   POST /api/auth/login
///     → Find admin by email
///     → Verify BCrypt password hash
///     → Generate signed JWT with role + companyId claims
///     → Return token (8h expiry)
/// </code>
/// <para><b>JWT claim structure:</b></para>
/// <list type="bullet">
///   <item><c>NameIdentifier</c> — Admin GUID, used to resolve the acting admin in controllers.</item>
///   <item><c>Role</c> — "Admin" or "SuperAdmin", drives <c>[Authorize(Roles=...)]</c> checks.</item>
///   <item><c>companyId</c> — Custom claim; empty string for SuperAdmins.</item>
/// </list>
/// <para><b>Security notes:</b></para>
/// <list type="bullet">
///   <item>Passwords are hashed with BCrypt (cost factor 11). Never stored in plain text.</item>
///   <item>Both "user not found" and "wrong password" return the same error message
///   to prevent email enumeration attacks.</item>
///   <item>JWT signing key must be at least 32 characters and stored in
///   <c>appsettings.Production.json</c> (which is gitignored).</item>
/// </list>
/// </remarks>
public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    /// <summary>
    /// Initialises a new instance of <see cref="AuthService"/>.
    /// </summary>
    /// <param name="db">The EF Core database context.</param>
    /// <param name="config">Application configuration (reads Jwt:Key, Jwt:Issuer, Jwt:Audience).</param>
    public AuthService(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    /// <summary>
    /// Authenticates an admin by email and password and returns a JWT token.
    /// </summary>
    /// <param name="request">Login credentials containing email and plain-text password.</param>
    /// <returns>
    /// A <see cref="LoginResponse"/> containing the signed JWT token, admin name,
    /// role string, and optional company ID.
    /// </returns>
    /// <exception cref="UnauthorizedAccessException">
    /// Thrown when the email is not found or the password does not match.
    /// The same exception type and message is used for both cases to prevent
    /// email enumeration.
    /// </exception>
    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        var admin = await _db.Admins
            .Include(a => a.Company)
            .FirstOrDefaultAsync(a => a.Email == request.Email)
            ?? throw new UnauthorizedAccessException("Invalid credentials.");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, admin.PasswordHash))
            throw new UnauthorizedAccessException("Invalid credentials.");

        var token = GenerateToken(admin);
        return new LoginResponse(token, admin.Name, admin.Role.ToString(), admin.CompanyId);
    }

    /// <summary>
    /// Registers a new admin account. Only accessible to SuperAdmins.
    /// </summary>
    /// <param name="request">
    /// Registration data including name, email, plain-text password, role, and optional company ID.
    /// </param>
    /// <returns>The created <see cref="AdminDto"/> (without the password hash).</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when an admin with the provided email already exists.
    /// </exception>
    /// <remarks>
    /// The plain-text password is hashed immediately on receipt and is never stored or logged.
    /// </remarks>
    public async Task<AdminDto> RegisterAsync(RegisterAdminRequest request)
    {
        if (await _db.Admins.AnyAsync(a => a.Email == request.Email))
            throw new InvalidOperationException("Email already registered.");

        var admin = new Admin
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = request.Role,
            CompanyId = request.CompanyId
        };

        _db.Admins.Add(admin);
        await _db.SaveChangesAsync();

        return new AdminDto(admin.Id, admin.Name, admin.Email,
            admin.Role.ToString(), admin.CompanyId);
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a signed JWT token for the given admin.
    /// Token expiry is 8 hours from the time of generation.
    /// </summary>
    /// <param name="admin">The authenticated admin entity.</param>
    /// <returns>A serialised, signed JWT string.</returns>
    private string GenerateToken(Admin admin)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, admin.Id.ToString()),
            new Claim(ClaimTypes.Email, admin.Email),
            new Claim(ClaimTypes.Name, admin.Name),
            new Claim(ClaimTypes.Role, admin.Role.ToString()),
            // companyId is empty string for SuperAdmins — never null in the claim
            new Claim("companyId", admin.CompanyId?.ToString() ?? string.Empty)
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}