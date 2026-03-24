# Cartridge Tracker

Internal web application for tracking cartridge states across large-scale facilities with multiple printers.

## Overview

Cartridge Tracker solves the problem of manual cartridge management in organizations with many heavily used printers. Administrators can scan QR codes from any device to instantly log cartridge state changes, send and receive refill batches, and access analytical statistics.

## Project Structure

```
CartridgeApp/
├── Domain/
│   ├── Entities/        # Core domain models (Company, Office, Printer, Cartridge, Action, Batch, Admin)
│   └── Enums/           # ActionType, BatchStatus, UserRole
├── Infrastructure/
│   └── Data/            # EF Core DbContext and migrations
├── Application/
│   ├── DTOs/            # Request and response models
│   ├── Interfaces/      # Service contracts
│   └── Services/        # Business logic implementations
├── API/
│   ├── Controllers/     # REST API endpoints
│   └── Middleware/      # Global error handling
├── Program.cs           # App entry point, DI registration
└── appsettings.json     # Configuration (no secrets committed)
```
## API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/auth/login` | Login, returns JWT token |
| POST | `/api/auth/register` | Register admin *(SuperAdmin only)* |
| GET | `/api/cartridges` | List cartridges |
| GET | `/api/cartridges/scan/{qrCode}` | Lookup by QR scan |
| POST | `/api/cartridges` | Create cartridge + generate QR |
| POST | `/api/actions` | Log cartridge action |
| GET | `/api/batches` | List refill batches |
| POST | `/api/batches` | Create and send batch |
| POST | `/api/batches/{id}/receive` | Receive batch back |
| GET | `/api/stats/dashboard` | Dashboard statistics |
