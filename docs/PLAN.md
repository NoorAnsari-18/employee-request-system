# Employee Request Management System — Plan (locked 2026-09-25)

Assignment: Automation & Systems Analyst, stage 2 (see `docs/assignment.pdf`, summary in `docs/assignment.md`).
Deadline: **Monday 2026-09-28**. Hard cap: **15 hours** total. Finish as early as possible.

## Architecture (hybrid)

```
Employee ─► Portal (ASP.NET Core .NET 10 Minimal API + React/Vite in wwwroot, Docker on Render)
              │ validate → classify (rules.json, AI fallback) → route (config) → SLA
              ├─ HTTPS REST (Private App token, HttpClient) ─► HubSpot CRM (single source of truth)
              │                                                   ▲        │ Zapier HubSpot triggers
              └─ (fallback) webhook on status change ─► Zapier ◄──┘────────┘
                                                        │ Z1/Z2 Gmail notifications
                                                        │ Z3 schedule → POST /api/escalations/run → manager alert
```

## Decisions

| # | Topic | Decision |
|---|---|---|
| Q2 | Architecture | Hybrid: coded portal + HubSpot + Zapier |
| Q3 | Stack/hosting | C# ASP.NET Core (.NET 10 LTS) Minimal API serving React (Vite) build from `wwwroot`; one Docker service on Render free; cron-job.org pings `/health` every 10 min |
| Q4 | Intercom | Diagram + summary only (future omni-channel intake with Email/WhatsApp/SMS) |
| Q5/Q5b | Categorization | Keyword rules first (`rules.json`). If confidence < 0.6 or top score < 2 → AI fallback (Claude Haiku 4.5) when `AI_TRIAGE_ENABLED=true`; on failure/timeout/no key → `Other`. Store `classified_by` = rules/ai/fallback. Build AI last in M2 |
| Q7 | Diagram | draw.io swimlanes: Employee / Portal+Logic / HubSpot / Zapier / Dept Agent / Manager |
| Q8 | Lifecycle UI | 3-column board (Open / Active / Finalized), one button per card: Start, Resolve (note required). Only Open→Active, Active→Finalized allowed |
| I1 | Data store | HubSpot only, no separate DB |
| I2 | Zap triggers | Zapier HubSpot triggers (polling) |
| I3/B6 | SLA engine | Zapier Schedule → POST `/api/escalations/run` (X-Api-Key) → C# does overdue logic |
| I4 | ID | `REQ-{DEPT}-{HubSpotTicketId}` stored in `request_id` (create then update) |
| I5 | Assignment | Routing table in config; all owners = candidate for demo; `department` acts as queue |
| I6 | Employee updates | Email (Zapier) + Track page |
| Q9 | SLA | High 4h / Medium 24h / Low 72h; `DEMO_MODE` → 5 / 15 / 30 min |
| Q10 | Priority | Employee urgency; keyword boosters can raise, never lower |
| B1 | Employee | **Upsert HubSpot Contact by email** (`/crm/v3/objects/contacts/batch/upsert`, idProperty=email) + create ticket with inline association (ticket→contact, HUBSPOT_DEFINED typeId 16 — verify). Also copy `employee_name`/`employee_email` onto ticket for Zapier |
| B2 | Form | Name*, Email*, Subject*, Description*, Urgency (Low/Medium/High, default Medium). No department field |
| B7 | Email sender | **Revised 2026-09-26:** "Email by Zapier" (send + robot.zapier.com intake) now; swap to a demo Gmail later if created. Demo owner/manager/employees use public @mailinator.com inboxes so reviewers can see emails |
| B8 | Board access | Open, "Demo – agent view" banner; SSO out of scope |
| B9 | Repo | Public GitHub, README, secrets only in Render env vars |
| B10 | Demo data | Seed script: ~8 tickets across departments and states |

### Omni-channel intake (decided 2026-09-26)

| # | Topic | Decision |
|---|---|---|
| O1 | Second real channel | **Email** → Zapier → `POST /api/requests` (`source_channel=Email`). Built in M4; **first cut** if M4 overruns. Twilio/WhatsApp and Intercom = roadmap |
| O2 | Inbox | Demo Gmail plus-address `<demo>+requests@gmail.com`; Zapier trigger "New Email Matching Search" `to:<demo>+requests@gmail.com` (prevents loops from our own outgoing mail) |
| O3 | Mapping | From name/address → employee + contact; subject → subject; body → description; urgency Medium (boosters may raise); same confirmation email |
| O4 | Channel trust | `source_channel` honoured only with valid `X-Api-Key`; otherwise forced to `Portal` |
| O5 | Diagram | All channels (Portal, Email, WhatsApp/SMS, Slack/Teams, Intercom) → "normalize" box; built = solid, planned = dashed, with legend |

Extensibility principle for the summary: "channel-agnostic: new channels are adapters, not rewrites".

### Integration & data flow (decided 2026-09-26)

| # | Topic | Decision |
|---|---|---|
| F1 | Writers | **Only the web app writes to HubSpot.** Zapier reads HubSpot (triggers) and calls the web app API when a change is needed |
| F2 | Edits in HubSpot UI | Allowed; HubSpot is source of truth; board reflects it; transition rules enforced only in portal (known gap, paid HubSpot can enforce) |
| F3 | Board refresh | Auto-refresh every 30s + manual refresh button |
| F4 | HubSpot webhooks | Not built; roadmap item (replace polling with push) |
| F5 | HubSpot failure | Retry 2× with backoff on 429/5xx, then friendly error + log. No queue (roadmap: durable queue) |
| F6 | Z2 trigger | Must use Zapier HubSpot trigger (catches HubSpot-UI edits). If no "ticket updated" trigger: Zapier Schedule every 5 min → web app endpoint returning tickets whose stage changed since last run |
| G1 | Platform choice | Keep current plan (portal + HubSpot free + Zapier). **Fallback:** if Zapier blocks > its 2h cap in M4, move notifications to C# (Gmail SMTP app password) and escalation trigger to cron-job.org, then ship |

Connections: (1) web app → HubSpot write, (2) web app ← HubSpot read (pull), (3) Zapier ← HubSpot (polling triggers, OAuth), (4) Zapier → web app (Webhooks, X-Api-Key), (5) web app → Zapier Catch Hook (only if needed), (6) Zapier → Gmail.

### Routing & assignment (decided 2026-09-26)

| # | Topic | Decision |
|---|---|---|
| C0 | Code vs workflow | Assignment is tool-agnostic ("algorithmic categorization"); code chosen (free tier, testable, rules.json editable). Justify in summary |
| C1 | Where routing lives | `routing.json` (department → team name, shared mailbox, HubSpot owner; manager email). Any value overridable on Render without code, e.g. `Routing__Teams__payroll__Email` |
| C2 | What email | Team (shared) mailboxes, not individuals → staff turnover handled by mailbox membership. Demo: `er-hr-team@`, `er-it-team@`, `er-payroll-team@`, `er-ops-team@`, `er-triage@` (all @mailinator.com) |
| C3 | Open tickets after routing change | Ticket stores `assigned_team` + `assigned_team_email` at creation (Z1 uses it); board, track and escalations resolve the team from the **current** routing table |
| C4 | Board | Team filter (All/HR/IT/Payroll/Ops/Triage), deep link `/board?team=<dept>` |
| C5 | Escalations | To manager (`Routing:ManagerEmail`), CC responsible teams (`ccEmails` in `/api/escalations/run` response) |

## HubSpot ticket properties

Standard: `subject`, `content`, `hs_pipeline`, `hs_pipeline_stage`, `hs_ticket_priority`, `hubspot_owner_id`.

Custom:

| Property | Type |
|---|---|
| `request_id` | single-line text |
| `department` | dropdown: HR / IT / Payroll / Operations / Other |
| `employee_name` | single-line text |
| `employee_email` | single-line text |
| `classified_by` | dropdown: rules / ai / fallback |
| `classification_confidence` | number |
| `sla_due_at` | date-time |
| `escalated` | single checkbox (yes/no) |
| `resolution_note` | multi-line text |
| `source_channel` | dropdown: Portal / Email / WhatsApp / Intercom |
| `assigned_team` | single-line text (team name at routing time) |
| `assigned_team_email` | single-line text (team mailbox at routing time) |

Priority: check if portal has URGENT; if not, escalation = HIGH + `escalated=true`.

## Classification rules (`rules.json`)

| Dept | Keywords (**bold** = weight 2) |
|---|---|
| HR | leave, vacation, holiday, sick, **maternity**, onboarding, policy, attendance, **resignation** |
| IT | laptop, **password**, **vpn**, login, software, printer, wifi, access, email account |
| Payroll | **salary**, **payslip**, reimbursement, tax, deduction, bonus, overtime, payment |
| Operations | facilities, office, desk, parking, travel, supplies, cleaning, booking, AC |

- Confidence = top score / total score. Rules decide if confidence ≥ 0.6 AND top score ≥ 2.
- Priority boosters → High: "salary not received", "can't login", "cannot access", "security", "breach", "system down", "urgent", "asap".
- xUnit tests with ~10 sample requests.

## API

| Method & path | Purpose |
|---|---|
| `POST /api/requests` | validate → classify → route → upsert contact → create ticket (+assoc) → set `request_id` → return `{requestId, department, priority, slaDueAt}`. Used by portal AND channel adapters (email via Zapier); `source_channel` only honoured with `X-Api-Key` |
| `GET /api/requests/{requestId}` | Track page |
| `GET /api/tickets` | Board data |
| `PATCH /api/tickets/{id}/status` | Open→Active, Active→Finalized (note required) |
| `POST /api/escalations/run` | X-Api-Key; find overdue, not escalated, not Finalized → HIGH/URGENT + `escalated=true`; return list |
| `GET /health` | keep-alive |

## Zaps

- **Z1** HubSpot New Ticket → Gmail to dept owner → Gmail confirmation to `employee_email`.
- **Z2** Stage change → Gmail to employee. If no HubSpot "ticket updated" trigger exists, portal POSTs to a Zapier Catch Hook on status change.
- **Z3** Schedule (hourly; 15 min in demo) → Webhooks POST `/api/escalations/run` → Filter count > 0 → Gmail to manager.
- Start the Zapier Pro trial at the beginning of M4 (multi-step, Filter, Webhooks need it).

## Milestones (sequential, no fixed dates, 15h cap)

| # | Milestone | Done when | Cap | Cut first if over |
|---|---|---|---|---|
| M1 | Setup | Accounts; HubSpot pipeline + properties + private app token verified | 2h | Non-core properties |
| M2 | Backend | POST creates classified ticket linked to contact, with ID; tests pass | 3.5h | AI fallback |
| M3 | UI + deploy | Form, board, track page live on public Render URL | 3h | Track page |
| M4 | Zapier | Z1, then Z2, then Z3 working | 2h | Z3 (describe in summary) |
| M5 | Diagram | draw.io link + PNG/PDF export | 1.5h | Polish |
| M6 | Summary | Candidate-written (non-AI) ≤2 pages, reviewed, PDF | 2h | Nothing |
| — | Buffer | | 1h | |

## Deliverables

1. Workflow diagram link/export (Part 1)
2. Public prototype URL + public GitHub repo (Part 2)
3. Summary PDF, ≤2 pages, **written by the candidate** (Part 3): assumptions, tool justification, automation/AI ROI nodes, scalability roadmap
