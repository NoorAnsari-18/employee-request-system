"""Generates docs/workflow.drawio: the request lifecycle as an overview page plus focused detail pages.

Run:  python scripts/make_diagram.py   then open the file at https://app.diagrams.net (pages = tabs at the bottom)
Solid = built in the PoC, dashed = designed / planned.
"""
from pathlib import Path
from xml.sax.saxutils import escape

W, H = 220, 80
LANE_H = 170
GUTTER = 190  # left column holding the lane titles

STYLE = {
    "app": "rounded=1;whiteSpace=wrap;html=1;fillColor=#dae8fc;strokeColor=#6c8ebf;fontSize=12;",
    "hubspot": "rounded=1;whiteSpace=wrap;html=1;fillColor=#ffe6cc;strokeColor=#d79b00;fontSize=12;",
    "zapier": "rounded=1;whiteSpace=wrap;html=1;fillColor=#f8cecc;strokeColor=#b85450;fontSize=12;",
    "team": "rounded=1;whiteSpace=wrap;html=1;fillColor=#d5e8d4;strokeColor=#82b366;fontSize=12;",
    "employee": "rounded=1;whiteSpace=wrap;html=1;fillColor=#ffffff;strokeColor=#666666;fontSize=12;",
    "manager": "rounded=1;whiteSpace=wrap;html=1;fillColor=#e1d5e7;strokeColor=#9673a6;fontSize=12;",
    "intake": "rounded=1;whiteSpace=wrap;html=1;fillColor=#dae8fc;strokeColor=#6c8ebf;fontSize=12;",
    "planned": "rounded=1;whiteSpace=wrap;html=1;dashed=1;fillColor=#f5f5f5;strokeColor=#999999;fontColor=#555555;fontSize=12;",
    "decision": "rhombus;whiteSpace=wrap;html=1;fillColor=#fff2cc;strokeColor=#d6b656;fontSize=11;",
    "note": "shape=note;whiteSpace=wrap;html=1;size=14;fillColor=#fffbe6;strokeColor=#d6b656;fontSize=11;align=left;spacingLeft=6;",
    "stage": "rounded=1;whiteSpace=wrap;html=1;fillColor=#6c8ebf;strokeColor=#314f7e;fontColor=#ffffff;fontSize=14;fontStyle=1;",
    "edge": "edgeStyle=orthogonalEdgeStyle;rounded=1;html=1;endArrow=block;endFill=1;strokeColor=#444444;fontSize=11;labelBackgroundColor=#ffffff;",
    "edge_planned": "edgeStyle=orthogonalEdgeStyle;rounded=1;html=1;endArrow=block;endFill=1;dashed=1;strokeColor=#999999;fontSize=11;fontColor=#666666;labelBackgroundColor=#ffffff;",
}

LANE_DEF = {
    "employee": ("Employee", "#f5f5f5"),
    "intake": ("Intake channels", "#fdf6e3"),
    "app": ("Web app\n(ASP.NET Core + React)", "#eaf2fb"),
    "hubspot": ("HubSpot CRM\n(system of record)", "#fff1e0"),
    "zapier": ("Zapier automations", "#fdecea"),
    "team": ("Responsible team\n(agent board)", "#eef7ea"),
    "manager": ("Manager", "#f3eefa"),
}

LEGEND = "Legend\n▭ solid = built in the PoC\n▭ dashed = designed / planned"


class Page:
    def __init__(self, name, title, lanes, cols):
        self.name, self.title, self.lanes = name, title, lanes
        self.width = GUTTER + cols * 260 + 40
        self.lane_y = {k: 90 + i * LANE_H for i, k in enumerate(lanes)}
        self.cells = []
        self._title()
        for k in lanes:
            self._lane(k)

    def col(self, i):
        return 20 + GUTTER + 20 + i * 260

    def _title(self):
        self.cells.append(
            f'<mxCell id="title" value="{escape(self.title)}" style="text;html=1;fontSize=22;fontStyle=1;align=left;" '
            f'vertex="1" parent="1"><mxGeometry x="20" y="25" width="{self.width}" height="40" as="geometry"/></mxCell>'
        )

    def _lane(self, key):
        label, fill = LANE_DEF[key]
        y = self.lane_y[key]
        self.cells += [
            f'<mxCell id="lane_{key}" value="" style="rounded=0;html=1;fillColor={fill};strokeColor=#bbbbbb;" vertex="1" parent="1">'
            f'<mxGeometry x="20" y="{y}" width="{self.width}" height="{LANE_H}" as="geometry"/></mxCell>',
            f'<mxCell id="lane_{key}_t" value="{escape(label)}" style="text;html=1;whiteSpace=wrap;fontStyle=1;fontSize=14;'
            f'align=center;verticalAlign=middle;" vertex="1" parent="1"><mxGeometry x="30" y="{y}" width="{GUTTER - 20}" '
            f'height="{LANE_H}" as="geometry"/></mxCell>',
            f'<mxCell id="lane_{key}_s" value="" style="endArrow=none;html=1;strokeColor=#bbbbbb;" edge="1" parent="1">'
            f'<mxGeometry relative="1" as="geometry"><mxPoint x="{20 + GUTTER}" y="{y}" as="sourcePoint"/>'
            f'<mxPoint x="{20 + GUTTER}" y="{y + LANE_H}" as="targetPoint"/></mxGeometry></mxCell>',
        ]

    def node(self, id_, lane, col, text, style=None, w=W, h=H, dy=0):
        y = self.lane_y[lane] + (LANE_H - h) // 2 + dy
        self.cells.append(
            f'<mxCell id="{id_}" value="{escape(text)}" style="{STYLE[style or lane]}" vertex="1" parent="1">'
            f'<mxGeometry x="{self.col(col)}" y="{y}" width="{w}" height="{h}" as="geometry"/></mxCell>'
        )

    def edge(self, src, dst, label="", planned=False):
        self.cells.append(
            f'<mxCell id="e_{src}_{dst}" value="{escape(label)}" style="{STYLE["edge_planned" if planned else "edge"]}" '
            f'edge="1" parent="1" source="{src}" target="{dst}"><mxGeometry relative="1" as="geometry"/></mxCell>'
        )

    def free(self, id_, x, y, text, style, w=W, h=H):
        self.cells.append(
            f'<mxCell id="{id_}" value="{escape(text)}" style="{STYLE[style]}" vertex="1" parent="1">'
            f'<mxGeometry x="{x}" y="{y}" width="{w}" height="{h}" as="geometry"/></mxCell>'
        )

    def xml(self, idx):
        height = 90 + len(self.lanes) * LANE_H + 60 if self.lanes else 700
        return (
            f'<diagram name="{escape(self.name)}" id="p{idx}"><mxGraphModel dx="1400" dy="800" grid="1" gridSize="10" '
            f'guides="1" tooltips="1" connect="1" arrows="1" fold="1" page="1" pageScale="1" '
            f'pageWidth="{self.width + 60}" pageHeight="{height}" math="0" shadow="0">'
            '<root><mxCell id="0"/><mxCell id="1" parent="0"/>' + "".join(self.cells) + "</root></mxGraphModel></diagram>"
        )


pages = []

# ---------------------------------------------------------------- 0. Overview (no lanes, 6 stages)
p = Page("0 · Overview", "Employee Request Management – lifecycle overview", [], 6)
stages = [
    ("S1", "1 · INTAKE", "One entry point for every channel\n(portal today; email, WhatsApp, Slack,\nIntercom as adapters)", "Web app"),
    ("S2", "2 · CATEGORISE", "Keyword rules → HR / IT / Payroll /\nOperations / Other + confidence;\nunclear → Triage Desk", "Web app"),
    ("S3", "3 · ROUTE & ASSIGN", "routing.json → responsible team\n+ shared mailbox; SLA set;\nticket + REQ-ID created", "Web app → HubSpot → Zapier (Z1)"),
    ("S4", "4 · WORK THE TICKET", "Open → Active → Finalized\non the team board;\nemployee kept informed", "Board → HubSpot → Zapier (Z2)"),
    ("S5", "5 · ESCALATE", "Hourly SLA check: overdue →\nURGENT + manager alerted\n(teams in CC)", "Zapier (Z3) → Web app → HubSpot"),
    ("S6", "6 · RESOLVE & ARCHIVE", "Finalized with resolution note;\nclosed ticket kept in HubSpot\nfor history & reporting", "HubSpot"),
]
for i, (sid, name, desc, where) in enumerate(stages):
    x = 40 + i * 290
    p.free(sid, x, 120, name, "stage", w=250, h=60)
    p.free(sid + "d", x, 200, desc, "app", w=250, h=110)
    p.free(sid + "w", x, 325, "Where: " + where, "note", w=250, h=50)
    p.cells.append(
        f'<mxCell id="{sid}_link" value="" style="endArrow=none;dashed=1;html=1;strokeColor=#6c8ebf;" edge="1" parent="1" '
        f'source="{sid}" target="{sid}d"><mxGeometry relative="1" as="geometry"/></mxCell>'
    )
    if i:
        p.edge(stages[i - 1][0], sid)
p.free("ov_note", 40, 420,
       "Pages 1–4 zoom into each stage:  1 · Intake & categorisation   2 · Routing, record & notification   "
       "3 · Lifecycle & resolution   4 · SLA escalation.\nSystem of record: HubSpot CRM. Only the web app writes to HubSpot; "
       "Zapier reads HubSpot events and calls the web app API.", "note", w=1700, h=70)
pages.append(p)

# ---------------------------------------------------------------- 1. Intake & categorisation
p = Page("1 · Intake & categorisation", "1 · Intake & categorisation – one entry point, rules-based classification",
         ["employee", "intake", "app"], 6)
p.node("E1", "employee", 0, "Employee raises a request\n(leave, IT issue, payroll, facilities…)")
p.node("I1", "intake", 0, "Web portal form\n(name, email, subject, description, urgency)", "intake")
p.node("I2", "intake", 1, "Email → Zapier adapter", "planned")
p.node("I3", "intake", 2, "WhatsApp / SMS → Twilio", "planned")
p.node("I4", "intake", 3, "Slack / Teams DM → Zapier", "planned")
p.node("I5", "intake", 4, "Intercom chat → Zapier", "planned")
p.edge("E1", "I1")
for i in ("I2", "I3", "I4", "I5"):
    p.edge("E1", i, planned=True)
p.node("L1", "app", 0, "POST /api/requests\nsingle entry point:\nvalidate & normalise")
p.node("L2", "app", 1, "Keyword rules (rules.json)\nscore each department\n→ confidence")
p.node("L3", "app", 2, "Confidence ≥ 0.6\nand score ≥ 2 ?", "decision", w=190, h=110)
p.node("L4", "app", 3, "AI triage (planned)\nelse → Other / Triage Desk", "planned")
p.node("L5", "app", 4, "Priority = urgency,\nraised by booster phrases\n(never lowered)")
p.edge("I1", "L1")
for i in ("I2", "I3", "I4", "I5"):
    p.edge(i, "L1", planned=True)
p.edge("L1", "L2")
p.edge("L2", "L3")
p.edge("L3", "L5", "yes → HR / IT / Payroll / Ops")
p.edge("L3", "L4", "no")
p.edge("L4", "L5")
p.free("leg1", p.col(5), p.lane_y["employee"] + 40, LEGEND, "note", w=220, h=80)
pages.append(p)

# ---------------------------------------------------------------- 2. Routing, record & notification
p = Page("2 · Routing, record & notification", "2 · Routing & assignment – ticket, unique ID and notifications",
         ["employee", "app", "hubspot", "zapier", "team"], 5)
p.node("R1", "app", 0, "Route via routing.json\ndepartment → team + shared mailbox\n(roles, not people)")
p.node("R2", "app", 1, "SLA due at\nHigh 4h · Medium 24h · Low 72h")
p.node("H1", "hubspot", 1, "Upsert Contact by email\n(employee history)")
p.node("H2", "hubspot", 2, "Create Ticket – OPEN\nowner · team · priority · SLA\nlinked to contact")
p.node("H3", "hubspot", 3, "Unique ID stored\nREQ-{DEPT}-{TicketID}")
p.node("Z1", "zapier", 3, "Z1 · New ticket\n→ email team + confirm employee")
p.node("T1", "team", 3, "Team mailbox notified\n→ /board?team=…")
p.node("E2", "employee", 3, "Confirmation email\nREQ-ID + track link")
p.node("E3", "employee", 4, "Tracks progress\n/track?id=REQ-…")
p.edge("R1", "R2")
p.edge("R2", "H1")
p.edge("H1", "H2")
p.edge("H2", "H3")
p.edge("H3", "Z1", "new ticket")
p.edge("Z1", "T1")
p.edge("Z1", "E2")
p.edge("E2", "E3")
p.free("n2", p.col(0), p.lane_y["team"] + 40,
       "Staff turnover: routing points to shared team mailboxes;\nany entry can be changed on Render without code\n(Routing__Teams__payroll__Email).", "note", w=440, h=80)
pages.append(p)

# ---------------------------------------------------------------- 3. Lifecycle & resolution
p = Page("3 · Lifecycle & resolution", "3 · Lifecycle & resolution – Open → Active → Finalized",
         ["team", "app", "hubspot", "zapier", "employee"], 5)
p.node("T1", "team", 0, "Opens team board\n(reads HubSpot live, 30 s refresh)")
p.node("T2", "team", 1, "Start")
p.node("T3", "team", 2, "Resolve with resolution note")
p.node("G1", "app", 1, "Lifecycle guard\nOpen → Active → Finalized only\nno skipping · note required")
p.node("H1", "hubspot", 1, "Stage = ACTIVE")
p.node("H2", "hubspot", 2, "Stage = FINALIZED (closed)\nresolution note stored")
p.node("H3", "hubspot", 3, "Archived & reportable\n(history per employee / team)")
p.node("Z2", "zapier", 2, "Z2 · Ticket status changed\n(filter: not Open)\n→ status email")
p.node("E1", "employee", 2, "‘Now being worked on’ /\n‘Resolved’ + resolution note")
p.edge("T1", "T2")
p.edge("T2", "T3")
p.edge("T2", "G1", "PATCH status")
p.edge("T3", "G1")
p.edge("G1", "H1", "valid move")
p.edge("G1", "H2")
p.edge("H2", "H3")
p.edge("H1", "Z2", "status change")
p.edge("H2", "Z2")
p.edge("Z2", "E1")
p.free("n3", p.col(3), p.lane_y["team"] + 35,
       "Edits made directly in HubSpot's own\nticket board also trigger Z2.", "note", w=260, h=70)
pages.append(p)

# ---------------------------------------------------------------- 4. SLA escalation
p = Page("4 · SLA escalation", "4 · SLA escalation – hourly sweep, manager alert",
         ["zapier", "app", "hubspot", "manager", "team"], 4)
p.node("Z1", "zapier", 0, "Z3 · Schedule\nevery hour")
p.node("A1", "app", 0, "POST /api/escalations/run\n(X-Api-Key)")
p.node("A2", "app", 1, "Find tickets past SLA,\nnot Finalized, not yet escalated")
p.node("H1", "hubspot", 1, "Priority → URGENT\nescalated = yes")
p.node("Z2", "zapier", 2, "count > 0 ?", "decision", w=170, h=100)
p.node("Z3", "zapier", 3, "Alert email to manager\n(CC responsible teams)")
p.node("M1", "manager", 3, "Reviews breaches\nreassigns / intervenes")
p.node("T1", "team", 3, "Team sees ‘Escalated’\nbadge on the board")
p.edge("Z1", "A1", "hourly")
p.edge("A1", "A2")
p.edge("A2", "H1", "escalate")
p.edge("A2", "Z2", "count, summary, emails")
p.edge("Z2", "Z3", "yes")
p.edge("Z3", "M1")
p.edge("Z3", "T1", "CC")
p.free("n4", p.col(2), p.lane_y["team"] + 35,
       "count = 0 → Zap stops, no email.\nEach ticket is escalated only once.", "note", w=240, h=70)
pages.append(p)

xml = '<mxfile host="app.diagrams.net">' + "".join(pg.xml(i) for i, pg in enumerate(pages)) + "</mxfile>"
out = Path(__file__).resolve().parent.parent / "docs" / "workflow.drawio"
out.write_text(xml, encoding="utf-8")
print(f"wrote {out} with {len(pages)} pages")
