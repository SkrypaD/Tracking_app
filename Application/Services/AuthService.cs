// <copyright file="AuthService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

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

public class AuthService : IAuthService
{
    private readonly AppDbContext db;
    private readonly IConfiguration config;

    public AuthService(AppDbContext db, IConfiguration config)
    {
        this.db = db;
        this.config = config;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        var admin = await this.db.Admins
            .Include(a => a.Company)
            .FirstOrDefaultAsync(a => a.Email == request.Email)
            ?? throw new UnauthorizedAccessException("Invalid credentials.");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, admin.PasswordHash))
        {
            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        var token = GenerateToken(admin);
        return new LoginResponse(token, admin.Name, admin.Role.ToString(), admin.CompanyId);
    }

    public async Task<AdminDto> RegisterAsync(RegisterAdminRequest request)
    {
        if (await this.db.Admins.AnyAsync(a => a.Email == request.Email))
        {
            throw new InvalidOperationException("Email already registered.");
        }

        var admin = new Admin
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = request.Role,
            CompanyId = request.CompanyId,
        };

        this.db.Admins.Add(admin);
        await this.db.SaveChangesAsync();

        return new AdminDto(admin.Id, admin.Name, admin.Email, admin.Role.ToString(), admin.CompanyId);
    }

    private string GenerateToken(Admin admin)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(this.config["Jwt:Key"] !));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, admin.Id.ToString()),
            new Claim(ClaimTypes.Email, admin.Email),
            new Claim(ClaimTypes.Name, admin.Name),
            new Claim(ClaimTypes.Role, admin.Role.ToString()),
            new Claim("companyId", admin.CompanyId?.ToString() ?? string.Empty),
        };

        var token = new JwtSecurityToken(
            issuer: this.config["Jwt:Issuer"],
            audience: this.config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
