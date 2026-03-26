# Architecture Decisions

This document records the key architectural and design decisions made during
development of Cartridge Tracker, along with the rationale behind each choice.

---

## ADR-001: Entity Hierarchy — Company anchored at Office level

**Decision:** `Office` holds both `CompanyId` and `BuildingId` as separate foreign keys.
`Building` has no `CompanyId`.

**Context:** A physical building can contain offices belonging to multiple companies
(e.g. a shared business centre). Anchoring the company at Building level would require
a many-to-many relationship or prevent this scenario entirely.

**Consequences:**
- `Building` remains a pure location entity with no ownership semantics ✓
- Company filtering always starts from `Office.CompanyId` ✓
- Two-level hierarchy (Company → Office) is clean and intuitive ✓

---

## ADR-002: Denormalise OfficeId onto Action

**Decision:** `CartridgeAction` stores `OfficeId` directly instead of resolving it
via `Cartridge → Printer → Office`.

**Context:** The most common dashboard query is "all actions for company X in date range Y".
Without denormalisation this requires a 5-join chain:
`Action → Cartridge → Printer → Office → Company`.

**Consequences:**
- Common queries use a single JOIN (`Action.OfficeId → Office.CompanyId`) ✓
- Slight redundancy — `OfficeId` is derivable from `Cartridge.Printer.OfficeId` ✗
- On printer reassignment (rare), historical actions correctly retain the original office ✓

---

## ADR-003: Actions are immutable

**Decision:** `CartridgeAction` records are never updated or deleted.
Corrections are made by creating a new action.

**Context:** The action table is the audit log of the system. Mutability would
undermine the ability to trace a cartridge's complete history.

**Consequences:**
- Full audit trail preserved ✓
- "Current state" must always be inferred from the most recent action ✓
- UI must communicate to users that corrections = new entry, not edit ✗

---

## ADR-004: QR code stored as base64 in database

**Decision:** QR codes are base64-encoded PNG images stored directly in the
`Cartridge.QrCode` column rather than in a file store.

**Context:** The project has no file storage component (see PRD Out of Scope).
Storing QR codes in the database avoids adding S3/local disk dependencies.

**Consequences:**
- Simple deployment — no file store needed ✓
- QR column increases row size (~3–5 KB per cartridge) ✗
- Not suitable if QR images are frequently re-requested at scale ✗
- Acceptable for current scale (< 10,000 cartridges) ✓

---

## ADR-005: No caching layer (v1)

**Decision:** No Redis or in-memory cache is added in v1.

**Context:** The system is an internal tool with low traffic (< 50 concurrent users).
Adding a cache would increase operational complexity without meaningful benefit.

**Planned for v2:** Cache the statistics dashboard response (5-minute TTL)
since it involves multiple aggregation queries.

---

## Component Interaction Map

```
┌────────────────────────────────────────────────────────┐
│  React Frontend (Vite)                                 │
│  ┌──────────┐  ┌────────────┐  ┌──────────────────────┐│
│  │ QR Scanner│  │ Dashboard  │  │ Batch Management    ││
│  └────┬─────┘  └─────┬──────┘  └──────────┬───────────┘│
└───────┼──────────────┼───────────────────┼─────────────┘
        │ GET /api/cartridges/scan/{qr}     │
        │              │ GET /api/stats/dashboard
        │              │                   │ POST /api/batches
        ▼              ▼                   ▼
┌─────────────────────────────────────────────────────────┐
│  ASP.NET Core API                                       │
│  ┌─────────────┐  ┌──────────────┐  ┌────────────────┐  │
│  │CartridgesCtrl│  │  StatsCtrl   │  │  BatchesCtrl  │  │
│  └──────┬──────┘  └──────┬───────┘  └───────┬────────┘  │
│         │                │                  │           │
│  ┌──────▼──────┐  ┌──────▼───────┐  ┌───────▼────────┐  │
│  │CartridgeSvc │  │  StatsSvc    │  │   BatchSvc     │  │
│  └──────┬──────┘  └──────┬───────┘  └───────┬────────┘  │
│         │                │                  │           │
│  ┌──────▼────────────────▼──────────────────▼────────┐  │
│  │               AppDbContext (EF Core)              │  │
│  └────────────────────────┬──────────────────────────┘  │
└───────────────────────────┼─────────────────────────────┘
                            │
                    ┌───────▼──────┐
                    │  PostgreSQL  │
                    └──────────────┘
```

---

## Business Logic: Suggested Next Action

When a cartridge is looked up via QR scan, the frontend pre-suggests the
most logical next action based on the last recorded state:

| Last Action | Suggested Next |
|-------------|---------------|
| (none / new) | Issued |
| Issued | Depleted |
| Depleted | Sent (via batch) |
| Sent | Refilled (via batch receive) |
| Refilled | Issued |

This logic lives in the frontend component, not in the API, because
it is a UI convenience feature — the API accepts any valid action regardless
of current state (admins can override the suggestion).