// <copyright file="Program.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Text;
using CartridgeApp.API.Middleware;
using CartridgeApp.Application.Interfaces;
using CartridgeApp.Application.Services;
using CartridgeApp.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;

// ── Bootstrap logger (used before DI is ready) ────────────────────────────────
// Мінімальний рівень логування можна змінити без перекомпіляції трьома способами:
//   1. Змінна оточення:  SERILOG__MINIMUMLEVEL__DEFAULT=Debug
//   2. Файл налаштувань: appsettings.json → "Serilog:MinimumLevel:Default"
//   3. Ключ при запуску: dotnet run --Serilog:MinimumLevel:Default=Debug
// ASP.NET Core читає всі три джерела автоматично через IConfiguration.
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting CartridgeApp...");

    var builder = WebApplication.CreateBuilder(args);

    // ── Serilog — читає конфігурацію з appsettings.json ───────────────────────
    // Рівень логування визначається в appsettings.json → "Serilog:MinimumLevel"
    // або через змінну оточення SERILOG__MINIMUMLEVEL__DEFAULT без перекомпіляції.
    builder.Host.UseSerilog((ctx, services, config) =>
        config
            .ReadFrom.Configuration(ctx.Configuration)   // appsettings.json / env vars
            .ReadFrom.Services(services)                  // дозволяє DI-enrichers
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "CartridgeApp"));

    // ── Database ──────────────────────────────────────────────────────────────
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

    // ── JWT Auth ──────────────────────────────────────────────────────────────
    var jwtKey = builder.Configuration["Jwt:Key"]!;
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidAudience = builder.Configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            };
        });

    builder.Services.AddAuthorization();

    // ── Services ──────────────────────────────────────────────────────────────
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<ICompanyService, CompanyService>();
    builder.Services.AddScoped<IBuildingService, BuildingService>();
    builder.Services.AddScoped<IOfficeService, OfficeService>();
    builder.Services.AddScoped<IPrinterService, PrinterService>();
    builder.Services.AddScoped<ICartridgeService, CartridgeService>();
    builder.Services.AddScoped<IBatchService, BatchService>();
    builder.Services.AddScoped<IStatsService, StatsService>();
    builder.Services.AddSingleton<IQrService>(_ =>
        new QrService(builder.Configuration["AppBaseUrl"] ?? "https://localhost:5001"));

    // ── In-memory cache (використовується CartridgeService та StatsService) ──
    // IMemoryCache є thread-safe singleton; розмір обмежено 1 000 записів.
    builder.Services.AddMemoryCache(options => options.SizeLimit = 1_000);

    // ── Performance monitor ────────────────────────────────────────────────────
    // Singleton — без стану, безпечний між потоками.
    builder.Services.AddSingleton<CartridgeApp.Application.Performance.IPerformanceMonitor,
        CartridgeApp.Application.Performance.PerformanceMonitor>();

    // ── Локалізація повідомлень про помилки ───────────────────────────────────
    builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

    // ── HttpContext для логування контексту запиту ────────────────────────────
    builder.Services.AddHttpContextAccessor();

    // ── CORS ──────────────────────────────────────────────────────────────────
    builder.Services.AddCors(options =>
        options.AddDefaultPolicy(policy =>
            policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

    // ── Swagger ───────────────────────────────────────────────────────────────
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo { Title = "Cartridge Tracker API", Version = "v1" });
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
        });
        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                },
                Array.Empty<string>()
            },
        });
    });

    builder.Services.AddControllers();

    var app = builder.Build();

    // ── Middleware pipeline ───────────────────────────────────────────────────
    // Serilog request logging — логує кожен HTTP запит з часом виконання
    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate =
            "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.000} ms";
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
            diagnosticContext.Set("UserId",
                httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous");
        };
    });

    app.UseMiddleware<ErrorHandlingMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseCors();
    app.UseHttpsRedirection();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    // ── Auto-apply migrations ─────────────────────────────────────────────────
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        try
        {
            logger.LogInformation("Applying database migrations...");
            await db.Database.MigrateAsync();
            logger.LogInformation("Database migrations applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Failed to apply database migrations. Application cannot start.");
            throw;
        }
    }

    Log.Information("CartridgeApp started successfully. Environment: {Environment}",
        builder.Environment.EnvironmentName);

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "CartridgeApp terminated unexpectedly.");
    throw;
}
finally
{
    Log.Information("CartridgeApp shutting down.");
    await Log.CloseAndFlushAsync();
}