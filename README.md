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

**Phase 0 — Foundation.** The solution, project wiring, and the Foundation domain
model (organisation hierarchy, RBAC, document versioning, append-only audit, the
generic workflow engine's entities) are scaffolded. The schema-per-organisation
tenant resolver and tenant-aware `DbContext` are in place and produce a verified EF
Core migration; they have not yet been run against a live database.

Next: Phase 1, the Requisition → Purchase Order vertical slice.

## Getting started

### Backend

Requires the .NET 10 SDK and a local PostgreSQL instance.

```bash
cd backend
dotnet build
dotnet run --project P2P.Api
```

The API expects an `X-Org-Code` header on every request except `/health` and
`/openapi` (a stand-in for the claim a real JWT will carry once auth is wired in).
Two organisations are seeded in `P2P.Api/appsettings.json` for local development:
`acme` and `globex`.

Applying `P2P.Infrastructure/Persistence/Migrations` to a real per-organisation
schema is not automated yet — see the Next steps checklist in
[`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md).

### Frontend

Requires Node.js 20+.

```bash
cd frontend
npm install
npm run dev
```
