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

**Phase 0 (Foundation) and Phase 1 (Requisition → PO vertical slice) are built and
verified against a real local PostgreSQL instance**, including the generic workflow
engine, maker-checker enforcement, and PO amendment with full version history. See
[`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) §6 for what's been verified and how.

Next: Phase 2 (Sourcing, Supplier, Contract) and Phase 4's multi-step workflow
support, or automating org provisioning (see the Next steps checklist in the
architecture doc).

## Getting started

### Backend

Requires the .NET 10 SDK and a local PostgreSQL instance.

```bash
cd backend
dotnet build
dotnet run --project P2P.Api
```

The API expects an `X-Org-Code` header on every request except `/health` and
`/openapi` (a stand-in for the claim a real JWT will carry once auth is wired in),
and an `X-User-Id` header (a stand-in for a user claim) on everything except the
diagnostic/seed endpoints. Two organisations are seeded in
`P2P.Api/appsettings.json` for local development: `acme` and `globex`. The frontend
handles both automatically via its organisation/user switcher.

The local Postgres connection string is **not** in `appsettings.json` (which only
has a non-functional placeholder) — set it with .NET user secrets:

```bash
cd backend
dotnet user-secrets set "ConnectionStrings:Postgres" "Host=localhost;Port=5432;Database=p2p_local;Username=p2p_app;Password=<your-local-password>" --project P2P.Api
```

Applying `P2P.Infrastructure/Persistence/Migrations` to a real per-organisation
schema is not automated yet (a manual `dotnet ef migrations script --idempotent` +
schema-name substitution + `psql` apply) — see the Next steps checklist in
[`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md). To try the app locally: start the
API and frontend, then just use the UI — it calls `POST
/api/v1/_diagnostics/seed-foundation` automatically on load for whichever
organisation is selected.

### Frontend

Requires Node.js 20+.

```bash
cd frontend
npm install
npm run dev
```
