# Zapier automations

Three Zaps turn HubSpot ticket events into notifications and run the hourly SLA check.

**Division of work**

| Part | Responsibility |
|---|---|
| **Web app** (ASP.NET Core) | Owns the rules: categorisation, routing, SLA, lifecycle guard, escalation logic. The **only** component that writes to HubSpot. |
| **HubSpot** | System of record: contacts, tickets, pipeline stages, history. |
| **Zapier** | Listens to HubSpot events and a schedule, then **delivers messages**. It never decides who is responsible; it reads that from the ticket or from the web app's reply. |

All three Zaps are published in the Zapier account (Pro trial, ends **2026-10-08**) and send email through **Gmail** as `physiquedemo@gmail.com` with the sender name *Employee Requests*.

| Zap | Purpose | Trigger | Zapier editor id |
|---|---|---|---|
| Z1 – New request notifications | Tell the responsible team and confirm to the employee | HubSpot · New Ticket | 381486400 |
| Z2 – Status update to employee | Tell the employee when work starts / finishes | HubSpot · New Ticket Property Change (Ticket status) | 381226602 |
| Z3 – Hourly SLA escalation | Escalate overdue tickets and alert the manager | Schedule · Every hour | 381521868 |

Reference IDs used below (HubSpot):

| Item | Value |
|---|---|
| Pipeline *Employee Requests* | `2590378726` |
| Stage Open | `4357009139` |
| Stage Active | `4357009140` |
| Stage Finalized | `4357009141` |

---

## Z1 – New request notifications

```
HubSpot: New Ticket ──► Filter: our pipeline ──► Gmail → responsible team ──► Gmail → employee
```

**1. Trigger – HubSpot · New Ticket** (polling, about every 2 minutes on the trial)

The trigger only returns standard ticket fields, so these custom properties are added under *Additional properties to retrieve*:

- Request ID
- Department
- Employee name
- Employee email
- Assigned team
- Assigned team email
- SLA due at
- Classified by
- Source channel

**2. Filter by Zapier** – only continue if *Pipeline* (Text) exactly matches `2590378726`. Tickets from HubSpot's default Support Pipeline are ignored.

**3. Gmail · Send Email → responsible team**

| Field | Value |
|---|---|
| To | *Assigned team email*, set by the web app from `routing.json`, e.g. `physiquedemo+payroll-team@gmail.com` |
| Subject | `New {Assigned team} request: {Request ID}, {Ticket name}` |
| Body | Request ID, subject, priority, employee name, SLA due, description, then *Work it on the board:* `https://employee-request-system.onrender.com/board?team={Department}` (opens that team's queue) |

**4. Gmail · Send Email → employee**

| Field | Value |
|---|---|
| To | *Employee email* |
| Subject | `We received your request {Request ID}` |
| Body | Greeting, request subject, *assigned to {Assigned team}*, priority, target resolution (SLA due), track link `…/track?id={Request ID}` |

**Why it is built like this**

- The team address is **on the ticket**, so routing stays in one place (`routing.json`, or Render environment overrides). The Zap never needs editing when a mailbox changes.
- The Filter keeps the Zap from reacting to tickets that don't belong to this system.

---

## Z2 – Status update to employee

```
HubSpot: Ticket status changed ──► Filter: our pipeline AND not Open ──► Formatter: ID → words ──► Gmail → employee
```

**1. Trigger – HubSpot · New Ticket Property Change**

- *Property Name* = **Ticket status** (`hs_pipeline_stage`).
- It fires on every stage change, whether made on our agent board **or** directly in HubSpot's own ticket board.
- *Properties to retrieve*: Ticket name, Ticket status, Pipeline, Assigned team, Resolution note, Employee email, Employee name, Request ID.

**2. Filter by Zapier** – continue only if:

- *Hs Pipeline* exactly matches `2590378726`, **and**
- *Hs Pipeline Stage* does **not** exactly match `4357009139` (Open).

The second rule matters: a new ticket is created in Open, and Z1 already confirms it, so the employee doesn't get two emails.

**3. Formatter by Zapier · Utilities · Lookup Table**

HubSpot sends the stage as an internal ID, so this step turns it into readable text.

| Lookup key (*Hs Pipeline Stage*) | Output |
|---|---|
| `4357009140` | now being worked on |
| `4357009141` | resolved |
| *(fallback)* | updated |

**4. Gmail · Send Email → employee**

| Field | Value |
|---|---|
| To | *Employee Email* |
| Subject | `Update on your request {Request Id}: {Output}` |
| Body | `Your request "{Subject}" is {Output}.`, *Handled by: {Assigned Team}*, track link, *Resolution note: {Resolution Note}* (empty while Active) |

**Why it is built like this**

- Watching the **property** (not "new ticket") catches changes made anywhere, so HubSpot stays the single source of truth.
- One lookup table and one template replace a paid *Paths* step with two email branches.

---

## Z3 – Hourly SLA escalation

```
Schedule: every hour ──► Webhook POST /api/escalations/run ──► Filter: count > 0 ──► Gmail → manager (CC teams)
```

**1. Trigger – Schedule by Zapier · Every Hour.** Zapier's shortest schedule is hourly.

**2. Webhooks by Zapier · POST**

| Setting | Value |
|---|---|
| URL | `https://employee-request-system.onrender.com/api/escalations/run` |
| Payload type | JSON (empty body) |
| Header | `X-Api-Key: <key>`. The key is **not** stored in this repo; see *Maintenance*. |

The web app does the work. It:

1. finds tickets in our pipeline that are past `sla_due_at`, not Finalized and not yet escalated,
2. sets each one to **Priority = URGENT** and **escalated = yes** (each ticket is escalated only once),
3. replies with the fields below, resolving team mailboxes from the **current** `routing.json`.

| Response field | Meaning |
|---|---|
| `count` | Number of tickets escalated in this run |
| `managerEmail` | Manager address from `routing.json` |
| `ccEmails` | Comma-separated mailboxes of the responsible teams |
| `summary` | One line per ticket: `REQ-… [Team] Subject (due …)` |
| `escalated[]` | Per ticket: id, requestId, subject, department, slaDueAt, teamName, teamEmail |

**3. Filter by Zapier** – continue only if *Count* (Number) is greater than `0`. In a quiet hour the Zap stops here and nothing is sent.

**4. Gmail · Send Email → manager**

| Field | Value |
|---|---|
| To | *Manager Email* |
| Cc | *Cc Emails* |
| Subject | `SLA breach: {Count} request(s) escalated` |
| Body | *The following employee requests breached their SLA and were escalated to URGENT:* `{Summary}`, then the board link |

**Why it is built like this**

- Date comparisons and HubSpot updates live in **tested C# code**, not in Zapier formulas.
- The HubSpot token exists in one place (the web app).
- Zapier contributes what it is good at: scheduling and delivery.
- The API key stops anyone else from triggering escalations.

---

## Maintenance & troubleshooting

| Task | How |
|---|---|
| See what a Zap did | Zapier → **Zap history** → open the run → *Data in / Data out* per step. Failed runs can be replayed. |
| Change a team or manager mailbox | Edit `src/EmployeeRequests.Api/routing.json` and push, **or** set e.g. `Routing__Teams__payroll__Email` / `Routing__ManagerEmail` in Render → Environment. No Zap change needed. |
| Add a new status stage | Add the stage in HubSpot, add a row to Z2's lookup table, and add it to the web app's `HubSpot:StageIds`. |
| Rotate the escalation API key | Generate a new random string. Set it in Render (`Security__ApiKey`) and locally (`dotnet user-secrets set "Security:ApiKey" …`). Paste it into Z3 step 2 → Headers → `X-Api-Key`, then republish Z3. The key is never committed; a local copy lives in git-ignored `api-key.local.txt`. |
| Reconnect Gmail or HubSpot | Zapier → **App connections** → the account → *Reconnect* (the account owner signs in). |
| Test without waiting | Z1/Z2: submit or move a ticket on the live site; the Zap runs within about 2 minutes. Z3: open the Zap → step 2 → *Test step* (this really escalates overdue tickets). |
| Temporarily stop emails | Turn the Zap's toggle off in the Zap list. Tickets keep flowing; nothing is lost. |
| After the trial ends (2026-10-08) | Either upgrade the Zapier plan, or use the documented fallback: send emails from the web app (SMTP) and trigger `/api/escalations/run` from cron-job.org. |
