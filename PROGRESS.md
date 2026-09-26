# Progress tracker

Update this file as each item is done. A new session resumes from the first unchecked item.

**Current status:** Planning complete, incl. omni-channel (O1–O5), data flow (F1–F6) and platform choice (G1) on 2026-09-26. Next: **M1 Setup**.

## M1 — Setup (cap 2h)
- [x] Create HubSpot free account
- [ ] Create demo Gmail account (for Zapier notifications)
- [ ] Create Render account (sign in with GitHub)
- [ ] Create cron-job.org account
- [ ] Create Zapier account (start Pro trial only at M4)
- [x] HubSpot: ticket pipeline "Employee Requests" with stages Open / Active / Finalized (created via connector; old default "Support Pipeline" id 0 left untouched, unused)
- [x] HubSpot: create custom ticket properties (verified via connector 2026-09-26; enum values are lowercase: hr/it/payroll/operations/other, portal/email/whatsapp/intercom, rules/ai/fallback)
- [x] HubSpot: priority URGENT exists (LOW/MEDIUM/HIGH/URGENT)
- [ ] HubSpot: Private App with scopes `tickets`, `crm.objects.contacts.read`, `crm.objects.contacts.write`, `crm.objects.owners.read`, `crm.schemas.tickets.read` (verify) → token saved locally in user-secrets (never commit)
- [x] Get your HubSpot owner ID
- [ ] Verify token with one test API call
- [x] Project skeleton: .NET 10 API (serves React build + /health) + React/Vite/TS (builds to wwwroot, dev proxy) + xUnit; git init; user-secrets init
- [ ] Create public GitHub repo and push

## M2 — Backend (cap 3.5h)
- [ ] rules.json + Classifier + unit tests
- [ ] Routing/SLA config (DEMO_MODE)
- [ ] HubSpot client: upsert contact, create ticket with association, update, search
- [ ] POST /api/requests, GET /api/requests/{id}, GET /api/tickets, PATCH status, POST escalations/run, GET /health
- [ ] X-Api-Key check (escalations + trusted source_channel); retry 2× on 429/5xx
- [ ] (stretch) AI fallback behind AI_TRIAGE_ENABLED

## M3 — UI + deploy (cap 3h)
- [ ] Submit form + confirmation with request ID
- [ ] 3-column board with Start / Resolve (note)
- [ ] Track page
- [ ] Dockerfile, deploy to Render, env vars
- [ ] cron-job.org ping /health
- [ ] Seed ~8 demo tickets

## M4 — Zapier (cap 2h)
- [ ] Start Zapier Pro trial; connect HubSpot + demo Gmail
- [ ] Z1 new ticket notifications
- [ ] Z2 stage-change notification (or webhook fallback)
- [ ] Z3 scheduled escalation
- [ ] Z4 email intake: Gmail `+requests` → POST /api/requests (first cut if over cap)
- [ ] Fallback (only if Zapier blocks > 2h): C# SMTP emails + cron-job.org escalation

## M5 — Diagram (cap 1.5h)
- [ ] draw.io swimlane diagram, shared link + PNG/PDF export

## M6 — Summary (cap 2h)
- [ ] Candidate writes draft (own words)
- [ ] Review, export PDF

## Submission
- [ ] Diagram link, prototype URL, repo URL, summary PDF sent

## Notes / IDs (fill in as you go, no secrets)
- HubSpot portal ID: 247520815
- Pipeline ID: 2590378726 (Employee Requests)
- Stage IDs: Open=4357009139 Active=4357009140 Finalized=4357009141
- Owner ID: 99794997
- Render URL:
- GitHub repo: https://github.com/NoorAnsari-18/employee-request-system (remote added)
