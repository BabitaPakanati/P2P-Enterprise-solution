# P2P Enterprise Solution

An enterprise-grade Procure-to-Pay platform: demand → requisition → sourcing →
supplier → contract → purchase order → receipt → invoice → matching → exceptions →
approval → payment → reconciliation → analytics.

Architecture, tenancy model, and the build roadmap are in
[`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) (a fully designed version is also
published [here](https://claude.ai/code/artifact/9e8c8a8b-8036-4c47-a188-c7003dd41f4e)).

## Repository layout

```
/backend    .NET 10 solution — Domain, Application, Infrastructure, Api, Workers, Tests
/frontend   React + TypeScript (Vite)
/infra      AWS CDK (C#) — not yet built, see docs/ARCHITECTURE.md §3 and §8
/docs       Architecture and planning docs
```

## Status

**Phase 0 (Foundation), Phase 1 (Requisition → PO vertical slice), a "harden" pass,
and a full root admin panel are built and verified against a real local PostgreSQL
instance**: the generic workflow engine, maker-checker enforcement, PO amendment
with full version history, a real `platform.organisations` tenant registry,
automated org provisioning (no more manual migration scripts), real JWT-based login,
a separate platform-admin identity tier for creating organisations from a UI, and
per-org configurable approval workflows and custom fields (with dependency support)
that the requisition/PO forms render dynamically. See
[`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) §6 and §9 for what's been verified
and how.

Next: Phase 2 (Sourcing, Supplier, Contract), Phase 3 (Receiving, Invoice, Matching),
or Phase 4's multi-step/escalation workflow support — see the Next steps checklist in
the architecture doc.

## Getting started

### Backend

Requires the .NET 10 SDK and a local PostgreSQL instance.

The local Postgres connection string and JWT signing key are **not** in
`appsettings.json` (which only has non-functional placeholders) — set them with
.NET user secrets:

```bash
cd backend
dotnet user-secrets set "ConnectionStrings:Postgres" "Host=localhost;Port=5432;Database=p2p_local;Username=p2p_app;Password=<your-local-password>" --project P2P.Api
dotnet user-secrets set "Jwt:SigningKey" "<a long random string, e.g. `openssl rand -base64 48`>" --project P2P.Api
```

```bash
cd backend
dotnet build
dotnet run --project P2P.Api
```

On first run, the API automatically creates and migrates its `platform` schema. Two
organisations (`acme`, `globex`) are pre-registered there via migration seed data;
provision their actual schemas (idempotent — safe to call again later, including for
a brand-new org):

```bash
curl -X POST http://localhost:5282/api/v1/platform/organisations \
  -H "Content-Type: application/json" -d '{"orgCode":"acme","displayName":"Acme Corporation"}'
curl -X POST http://localhost:5282/api/v1/platform/organisations \
  -H "Content-Type: application/json" -d '{"orgCode":"globex","displayName":"Globex Corporation"}'
```

Every endpoint except `/health`, `/openapi`, `/api/v1/auth/login`, and two dev-only
bootstrap diagnostics requires a valid JWT (`Authorization: Bearer <token>`), issued
by `POST /api/v1/auth/login` (body `{email, password}`, header `X-Org-Code`). To get
a user to log in as, seed one first:

```bash
curl -X POST http://localhost:5282/api/v1/_diagnostics/seed-foundation -H "X-Org-Code: acme"
```

This creates two demo users for the org — `requester@acme.example` and
`approver@acme.example`, both with password `P2pDemo!2026` (see
`FoundationSeeder.DevPassword`). The frontend's login page has a "Seed demo data"
button that does this for you and shows the credentials.

### Frontend

Requires Node.js 20+.

```bash
cd frontend
npm install
npm run dev
```
