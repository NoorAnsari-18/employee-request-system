# Employee Request Management System (PoC)

A single intake point for employee requests (leave, IT, payroll, facilities) that **categorises, routes, tracks and escalates** them automatically, using **HubSpot CRM** as the system of record and **Zapier** for notifications and scheduling.

- **Live demo:** _add Render URL_
- **Pages:** `/` submit a request · `/track` check status by ID · `/board` agent board (Open → Active → Finalized)

## Architecture

```
Employee ─► Web app (ASP.NET Core .NET 10 + React)          ◄── Zapier (schedule, email intake)
              validate → classify (rules.json) → route → SLA        X-Api-Key
              │
              ├─ REST (Private App token) ─► HubSpot CRM: Contact + Ticket (single source of truth)
              │                                   │
              │                                   └─► Zapier HubSpot triggers ─► Gmail notifications
```

- **One writer:** only the web app writes to HubSpot, so business rules live in one tested place. Zapier reads HubSpot and calls the API.
- **No local database:** every screen reads HubSpot live, so changes made in HubSpot's own UI show up here too.
- **Channel-agnostic intake:** `POST /api/requests` is the single entry point. The portal form is one adapter; email (via Zapier) is another. New channels are adapters, not rewrites.

## How a request flows

1. `POST /api/requests` validates input, then the **keyword classifier** scores each department (`rules.json`). Rules decide when confidence ≥ 0.6 and score ≥ 2; otherwise the ticket goes to **Other / needs triage**.
2. **Priority** starts at the employee's urgency; booster phrases ("salary not received", "cannot access", "security"…) raise it to High, never lower it.
3. **Routing** assigns the responsible team from [`routing.json`](src/EmployeeRequests.Api/routing.json): each department maps to a team name and a **shared team mailbox** (roles, not people, so staff turnover doesn't break routing). Any entry can be changed without code via environment variables, e.g. `Routing__Teams__payroll__Email`; **SLA** sets `sla_due_at` from priority (4h / 24h / 72h, or 5 / 15 / 30 min in `Sla:DemoMode`).
4. The employee's HubSpot **contact** is upserted by email and the **ticket** is created in the *Employee Requests* pipeline, associated with that contact.
5. The public ID `REQ-{DEPT}-{HubSpotTicketId}` (e.g. `REQ-IT-336967343854`) is stored on the ticket.
6. Agents move tickets **Open → Active → Finalized** on `/board`; skipping or reopening is rejected (409), and finalizing requires a resolution note.
7. Zapier calls `POST /api/escalations/run` on a schedule; overdue, unresolved tickets become **URGENT** + `escalated`, and the manager is alerted.

## API

| Method | Path | Purpose |
|---|---|---|
| POST | `/api/requests` | Submit a request (portal and channel adapters) |
| GET | `/api/requests/{requestId}` | Public status lookup |
| GET | `/api/tickets` | Board data |
| PATCH | `/api/tickets/{id}/status` | `{ "status": "Active" \| "Finalized", "resolutionNote": "…" }` |
| POST | `/api/escalations/run` | Escalate overdue tickets (requires `X-Api-Key`) |
| GET | `/api/teams` | Team list for the board filter |
| GET | `/health` | Keep-alive |

## Run locally

Prerequisites: .NET 10 SDK, Node 20+, a HubSpot private app token (scopes: `tickets`, `crm.objects.contacts.read/write`, `crm.objects.owners.read`).

```bash
dotnet user-secrets set "HubSpot:Token" "<token>" --project src/EmployeeRequests.Api
dotnet user-secrets set "Security:ApiKey" "<any long random string>" --project src/EmployeeRequests.Api
cd web && npm ci && npm run build && cd ..
dotnet run --project src/EmployeeRequests.Api     # http://localhost:5293
```

Frontend with hot reload: `cd web && npm run dev` (http://localhost:5173, proxies `/api` to 5293).
Tests: `dotnet test`. Demo data: `.\scripts\seed.ps1 [-BaseUrl <url>]`.

## Deploy (Render)

Docker web service from this repo (`Dockerfile` builds React, then the API). Environment variables: `HubSpot__Token`, `Security__ApiKey`. A cron-job.org ping to `/health` every 10 minutes keeps the free instance awake.

## Project layout

```
src/EmployeeRequests.Api/
  Domain/        Classifier, lifecycle rules, routing & SLA
  HubSpot/       HubSpot REST client (only HubSpot writer) + retry handler
  Endpoints/     Minimal API endpoints
  rules.json     Department keywords, weights, priority boosters
web/src/         React pages: submit, track, board
tests/           xUnit tests for classifier, priority, lifecycle, SLA
scripts/seed.ps1 Demo data through the public API
```
