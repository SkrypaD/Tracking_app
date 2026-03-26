# Cartridge Tracker

Internal web application for tracking cartridge states across large-scale facilities with multiple printers.

## Overview

Cartridge Tracker solves the problem of manual cartridge management in organizations with many heavily used printers. Administrators can scan QR codes from any device to instantly log cartridge state changes, send and receive refill batches, and access analytical statistics.

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



# Cartridge Tracker

Internal web application for tracking cartridge states across large-scale facilities with multiple printers.

## Architecture Overview

```
┌─────────────────────────────────────────────────────────┐
│                        CLIENT                           │
│              React + Vite (port 5173 dev)               │
│              Nginx (port 80/443 prod)                   │
└─────────────────────┬───────────────────────────────────┘
                      │ HTTP / REST
┌─────────────────────▼───────────────────────────────────┐
│                   APPLICATION SERVER                    │
│           ASP.NET Core 8 Web API (port 5001)            │
│   Auth (JWT) │ Controllers │ Services │ EF Core         │
└─────────────────────┬───────────────────────────────────┘
                      │ TCP 5432
┌─────────────────────▼───────────────────────────────────┐
│                       DATABASE                          │
│                  PostgreSQL 16                          │
│             Database: cartridge_db                      │
└─────────────────────────────────────────────────────────┘
```

**Components:**
- **Web server:** Nginx (production reverse proxy + static file serving)
- **Application server:** ASP.NET Core 8 Kestrel
- **Database:** PostgreSQL 16
- **No caching layer** (planned for v2)
- **No file storage** (QR codes stored as base64 in DB)

---

## Developer Setup (Fresh OS)

### Prerequisites

Install the following in order:

**1. .NET 8 SDK**
```bash
# Ubuntu/Debian
wget https://dot.net/v1/dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 8.0
echo 'export PATH=$PATH:$HOME/.dotnet' >> ~/.bashrc && source ~/.bashrc

# Windows — download installer from https://dotnet.microsoft.com/download/dotnet/8.0
# macOS
brew install dotnet@8
```

**2. PostgreSQL 16**
```bash
# Ubuntu/Debian
sudo apt update && sudo apt install -y postgresql postgresql-contrib

# macOS
brew install postgresql@16
brew services start postgresql@16

# Windows — download from https://www.postgresql.org/download/windows/
```

**3. Node.js 20+ (for frontend)**
```bash
# Ubuntu/Debian
curl -fsSL https://deb.nodesource.com/setup_20.x | sudo -E bash -
sudo apt install -y nodejs

# macOS
brew install node@20

# Windows — download from https://nodejs.org/
```

**4. Git**
```bash
# Ubuntu/Debian
sudo apt install -y git

# macOS
brew install git
```

---

### 1. Clone the repository

```bash
git clone https://github.com/yourusername/cartridge-tracker.git
cd cartridge-tracker
```

### 2. Configure the database

```bash
# Create the database and user
sudo -u postgres psql <<EOF
CREATE DATABASE cartridge_db;
CREATE USER cartridge_user WITH ENCRYPTED PASSWORD 'devpassword123';
GRANT ALL PRIVILEGES ON DATABASE cartridge_db TO cartridge_user;
EOF
```

### 3. Configure the backend

Create `appsettings.Development.json` in the project root (this file is gitignored):

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=cartridge_db;Username=cartridge_user;Password=devpassword123"
  },
  "Jwt": {
    "Key": "dev-secret-key-change-in-production-32chars",
    "Issuer": "CartridgeApp",
    "Audience": "CartridgeApp"
  },
  "AppBaseUrl": "http://localhost:5001"
}
```

### 4. Install dependencies and run migrations

```bash
# Restore NuGet packages
dotnet restore

# Apply database migrations (creates all tables + seed data)
dotnet ef database update
```

If `dotnet ef` is not found:
```bash
dotnet tool install --global dotnet-ef
```

### 5. Run the backend

```bash
dotnet run
# API available at https://localhost:5001
# Swagger UI at https://localhost:5001/swagger
```

### 6. Run the frontend (separate terminal)

```bash
cd frontend
npm install
npm run dev
# App available at http://localhost:5173
```

### 7. Create the first SuperAdmin

Since registration requires a SuperAdmin token, seed one manually:

```bash
sudo -u postgres psql -d cartridge_db <<EOF
INSERT INTO "Admins" ("Id", "Name", "Email", "PasswordHash", "Role", "CompanyId")
VALUES (
  gen_random_uuid(),
  'Super Admin',
  'admin@example.com',
  -- BCrypt hash of 'Admin1234!'
  '\$2a\$11\$rBnqBm6kB/G0Ux8KqHk2N.QT4Vd1Jz6Xv3TcY7wRpLsEuAiOmFdK',
  2,
  NULL
);
EOF
```

Then login via `POST /api/auth/login` with `admin@example.com` / `Admin1234!`.

---

## Common Commands

```bash
# Run backend
dotnet run

# Run with hot reload
dotnet watch run

# Add a new migration
dotnet ef migrations add MigrationName

# Apply migrations
dotnet ef database update

# Rollback last migration
dotnet ef migrations remove

# Run frontend
cd frontend && npm run dev

# Build frontend for production
cd frontend && npm run build

# Run dev environment (backend + frontend together)
./scripts/dev.sh
```


# Documentation Standards

This section defines how all contributors should document code in this project.

---

## What to Document

| What | Required tags | When |
|------|--------------|------|
| Every public class | `<summary>`, `<remarks>` (if non-trivial) | Always |
| Every public method | `<summary>`, `<param>`, `<returns>`, `<exception>` | Always |
| Private helpers | `<summary>` (one line) | If non-obvious |
| Architectural decisions | `docs/architecture.md` ADR entry | When making a significant design choice |
| Business logic | `<remarks>` with state diagrams or flow descriptions | When logic is non-trivial |

---

## XML Comment Format (C#)

Minimum for a simple method:
```csharp
/// <summary>Returns a cartridge by its unique identifier.</summary>
/// <param name="id">The cartridge GUID.</param>
/// <returns>A <see cref="CartridgeDto"/> for the requested cartridge.</returns>
/// <exception cref="KeyNotFoundException">When no cartridge with this ID exists.</exception>
public async Task<CartridgeDto> GetByIdAsync(Guid id) { ... }
```

For complex classes with business logic:
```csharp
/// <summary>One-line description.</summary>
/// <remarks>
/// Explain WHY this exists and HOW it fits in the system.
/// Include state diagrams using <code> blocks when relevant.
/// Reference related types with <see cref="RelatedClass"/>.
/// </remarks>
public class BatchService { ... }
```

---

## Rules

1. **Every new public member must have a `<summary>` before the PR is merged.**
2. **Update the doc comment in the same commit as the code change.**
3. **Use `<see cref="..."/>` to link related types** — keeps docs navigable.
4. **Document the WHY, not just the WHAT.** The code already shows what it does.
5. **Record architectural decisions** in `docs/architecture.md` as ADR entries.

---

## Generating Documentation

See `docs/generate_docs.md` for full instructions. Quick version:

```bash
dotnet tool install -g docfx
cd docfx && docfx --serve
```

---

## Checking Documentation Quality

```bash
# Check for undocumented public members (CS1591 warnings)
dotnet build 2>&1 | grep "CS1591"
```

Zero CS1591 warnings is the target before merging to `main`.