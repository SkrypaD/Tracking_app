// <copyright file="AppDbContext.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using CartridgeApp.Domain.Entities;

// using CartridgeApp.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CartridgeApp.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Company> Companies => this.Set<Company>();

    public DbSet<Building> Buildings => this.Set<Building>();

    public DbSet<Office> Offices => this.Set<Office>();

    public DbSet<PrinterType> PrinterTypes => this.Set<PrinterType>();

    public DbSet<Printer> Printers => this.Set<Printer>();

    public DbSet<CartridgeType> CartridgeTypes => this.Set<CartridgeType>();

    public DbSet<Cartridge> Cartridges => this.Set<Cartridge>();

    public DbSet<CartridgeAction> Actions => this.Set<CartridgeAction>();

    public DbSet<Batch> Batches => this.Set<Batch>();

    public DbSet<Admin> Admins => this.Set<Admin>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Company
        modelBuilder.Entity<Company>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
        });

        // Building
        modelBuilder.Entity<Building>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.Address).IsRequired().HasMaxLength(500);
        });

        // Office — belongs to both Company and Building
        modelBuilder.Entity<Office>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.HasOne(x => x.Company).WithMany(x => x.Offices).HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Building).WithMany(x => x.Offices).HasForeignKey(x => x.BuildingId).OnDelete(DeleteBehavior.Restrict);
        });

        // PrinterType
        modelBuilder.Entity<PrinterType>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(100);
        });

        // Printer
        modelBuilder.Entity<Printer>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.HostIp).HasMaxLength(50);
            e.HasOne(x => x.Office).WithMany(x => x.Printers).HasForeignKey(x => x.OfficeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.PrinterType).WithMany(x => x.Printers).HasForeignKey(x => x.PrinterTypeId).OnDelete(DeleteBehavior.Restrict);
        });

        // CartridgeType
        modelBuilder.Entity<CartridgeType>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(100);
        });

        // Cartridge
        modelBuilder.Entity<Cartridge>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.QrCode).IsRequired();
            e.HasOne(x => x.Printer).WithMany(x => x.Cartridges).HasForeignKey(x => x.PrinterId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CartridgeType).WithMany(x => x.Cartridges).HasForeignKey(x => x.CartridgeTypeId).OnDelete(DeleteBehavior.Restrict);
        });

        // CartridgeAction — immutable, no cascade deletes
        modelBuilder.Entity<CartridgeAction>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ActionType).HasConversion<int>();
            e.HasOne(x => x.Cartridge).WithMany(x => x.Actions).HasForeignKey(x => x.CartridgeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Office).WithMany().HasForeignKey(x => x.OfficeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Printer).WithMany().HasForeignKey(x => x.PrinterId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Admin).WithMany(x => x.Actions).HasForeignKey(x => x.AdminId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Batch).WithMany(x => x.Actions).HasForeignKey(x => x.BatchId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
        });

        // Batch
        modelBuilder.Entity<Batch>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion<int>();
            e.HasOne(x => x.Admin).WithMany(x => x.Batches).HasForeignKey(x => x.AdminId).OnDelete(DeleteBehavior.Restrict);
        });

        // Admin
        modelBuilder.Entity<Admin>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Email).IsRequired().HasMaxLength(200);
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Role).HasConversion<int>();
            e.HasOne(x => x.Company).WithMany(x => x.Admins).HasForeignKey(x => x.CompanyId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        });


        // ── Performance indexes ────────────────────────────────────────────────
        //
        // These indexes were identified as necessary after profiling the three
        // hottest queries in the application:
        //
        // 1. CartridgeAction.CreatedAt — used in every date-range filter on the
        //    actions list and in the monthly stats counters (GetDashboardAsync).
        //    Without this index, PostgreSQL performs a full sequential scan of the
        //    actions table on every dashboard load.
        //
        // 2. CartridgeAction.CartridgeId + CreatedAt (composite) — the "currently
        //    sent" subquery in StatsService orders actions per cartridge by date.
        //    A composite index lets PostgreSQL satisfy the WHERE + ORDER BY with
        //    a single index-only scan instead of a sort.
        //
        // 3. CartridgeAction.OfficeId — used in company-scoped filters on the
        //    actions table (GetAllAsync with companyId filter).  The OfficeId is
        //    already denormalised onto the action row for exactly this reason.
        //
        // 4. Cartridge.PrinterId — used in the most common cartridge list query
        //    (GET /api/cartridges?printerId=...).
        modelBuilder.Entity<CartridgeAction>(e =>
        {
            e.HasIndex(x => x.CreatedAt)
                .HasDatabaseName("IX_Actions_CreatedAt");

            e.HasIndex(x => new { x.CartridgeId, x.CreatedAt })
                .HasDatabaseName("IX_Actions_CartridgeId_CreatedAt");

            e.HasIndex(x => x.OfficeId)
                .HasDatabaseName("IX_Actions_OfficeId");
        });

        modelBuilder.Entity<Cartridge>(e =>
        {
            e.HasIndex(x => x.PrinterId)
                .HasDatabaseName("IX_Cartridges_PrinterId");
        });

                // Seed lookup data
        modelBuilder.Entity<PrinterType>().HasData(
            new PrinterType { Id = Guid.Parse("00000000-0000-0000-0000-000000000001"), Name = "Laser" },
            new PrinterType { Id = Guid.Parse("00000000-0000-0000-0000-000000000002"), Name = "Inkjet" });

        modelBuilder.Entity<CartridgeType>().HasData(
            new CartridgeType { Id = Guid.Parse("00000000-0000-0000-0000-000000000011"), Name = "Black Toner" },
            new CartridgeType { Id = Guid.Parse("00000000-0000-0000-0000-000000000012"), Name = "Color Toner" },
            new CartridgeType { Id = Guid.Parse("00000000-0000-0000-0000-000000000013"), Name = "Black Ink" },
            new CartridgeType { Id = Guid.Parse("00000000-0000-0000-0000-000000000014"), Name = "Color Ink" });
    }
}