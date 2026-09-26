import { useCallback, useEffect, useState } from 'react'
import { api, ApiError, departmentLabel, formatDate, slaLabel, type BoardTicket, type Status, type Team } from '../api'

const columns: { status: Status; title: string; hint: string }[] = [
  { status: 'Open', title: 'Open', hint: 'New, not yet picked up' },
  { status: 'Active', title: 'Active', hint: 'Being worked on' },
  { status: 'Finalized', title: 'Finalized', hint: 'Resolved with a note' },
]

const REFRESH_MS = 30_000

export default function BoardPage() {
  const [tickets, setTickets] = useState<BoardTicket[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [updatedAt, setUpdatedAt] = useState<Date | null>(null)
  const [teams, setTeams] = useState<Team[]>([])
  // ?team=payroll lets each team's notification email open straight into its own queue.
  const [team, setTeam] = useState(() => new URLSearchParams(window.location.search).get('team') ?? 'all')

  useEffect(() => {
    api.teams().then(setTeams).catch(() => setTeams([]))
  }, [])

  const chooseTeam = (key: string) => {
    setTeam(key)
    window.history.replaceState({}, '', key === 'all' ? '/board' : `/board?team=${key}`)
  }

  // Always read live from HubSpot, so changes made directly in HubSpot show up here too.
  const load = useCallback(async () => {
    try {
      setTickets(await api.tickets())
      setUpdatedAt(new Date())
      setError(null)
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Could not load tickets.')
    }
  }, [])

  useEffect(() => {
    load()
    const timer = setInterval(load, REFRESH_MS)
    return () => clearInterval(timer)
  }, [load])

  const replace = (t: BoardTicket) => setTickets((all) => all?.map((x) => (x.id === t.id ? t : x)) ?? null)

  return (
    <section>
      <div className="demo-banner">
        Demo – agent view. Open to reviewers without sign-in; production would use company SSO.
      </div>
      <div className="board-header">
        <div>
          <h1>Agent board</h1>
          <p className="muted">
            Tickets live in HubSpot. Refreshes every 30 s{updatedAt && ` · updated ${updatedAt.toLocaleTimeString()}`}
          </p>
        </div>
        <button className="button secondary" onClick={load}>
          Refresh
        </button>
      </div>
      <div className="team-filter" role="tablist" aria-label="Filter by team">
        {[{ key: 'all', name: 'All teams' }, ...teams].map((t) => (
          <button
            key={t.key}
            role="tab"
            aria-selected={team === t.key}
            className={team === t.key ? 'active' : ''}
            onClick={() => chooseTeam(t.key)}
          >
            {t.name}
          </button>
        ))}
      </div>
      {error && <p className="alert">{error}</p>}
      {!tickets && !error && <p className="muted">Loading tickets…</p>}
      {tickets && (
        <div className="board">
          {columns.map((col) => {
            const items = tickets.filter((t) => t.status === col.status && (team === 'all' || t.department === team))
            return (
              <div key={col.status} className={`column col-${col.status.toLowerCase()}`}>
                <div className="column-head">
                  <h2>{col.title}</h2>
                  <span className="count">{items.length}</span>
                </div>
                <p className="column-hint">{col.hint}</p>
                {items.length === 0 && <p className="empty">No tickets</p>}
                {items.map((t) => (
                  <TicketCard key={t.id} ticket={t} onChanged={replace} />
                ))}
              </div>
            )
          })}
        </div>
      )}
    </section>
  )
}

function TicketCard({ ticket: t, onChanged }: { ticket: BoardTicket; onChanged: (t: BoardTicket) => void }) {
  const [resolving, setResolving] = useState(false)
  const [note, setNote] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function move(status: Status, resolutionNote?: string) {
    setBusy(true)
    setError(null)
    try {
      onChanged(await api.changeStatus(t.id, status, resolutionNote))
      setResolving(false)
    } catch (err) {
      setError(err instanceof ApiError ? (Object.values(err.fieldErrors)[0]?.[0] ?? err.message) : 'Update failed.')
    } finally {
      setBusy(false)
    }
  }

  const sla = slaLabel(t.slaDueAt)

  return (
    <article className={`ticket ${t.escalated ? 'escalated' : ''}`}>
      <div className="ticket-top">
        <span className="ticket-id">{t.requestId ?? `#${t.id}`}</span>
        <span className={`badge prio-${t.priority.toLowerCase()}`}>{t.priority}</span>
      </div>
      <h3>{t.subject}</h3>
      <div className="tags">
        <span className="badge dept">{departmentLabel[t.department] ?? t.department}</span>
        {t.classifiedBy === 'fallback' && <span className="badge warn">Needs triage</span>}
        {t.escalated && <span className="badge danger">Escalated</span>}
        {t.sourceChannel && t.sourceChannel !== 'portal' && <span className="badge">via {t.sourceChannel}</span>}
      </div>
      <p className="ticket-meta">
        Assigned to <strong>{t.assignedTeam}</strong>
      </p>
      <p className="ticket-meta">
        {t.employeeName ?? 'Unknown'} · {formatDate(t.createdAt)}
      </p>
      {t.status !== 'Finalized' && <p className={`sla ${sla.overdue ? 'overdue' : ''}`}>SLA {sla.text}</p>}
      {t.status === 'Finalized' && t.resolutionNote && <p className="resolution">✔ {t.resolutionNote}</p>}

      {t.status === 'Open' && (
        <button className="button small" disabled={busy} onClick={() => move('Active')}>
          {busy ? 'Starting…' : 'Start'}
        </button>
      )}
      {t.status === 'Active' && !resolving && (
        <button className="button small" onClick={() => setResolving(true)}>
          Resolve
        </button>
      )}
      {t.status === 'Active' && resolving && (
        <div className="resolve-box">
          <textarea
            rows={3}
            value={note}
            onChange={(e) => setNote(e.target.value)}
            placeholder="How was this resolved? (required)"
            autoFocus
          />
          <div className="actions">
            <button className="button small" disabled={busy || !note.trim()} onClick={() => move('Finalized', note)}>
              {busy ? 'Saving…' : 'Finalize'}
            </button>
            <button className="button small secondary" onClick={() => setResolving(false)}>
              Cancel
            </button>
          </div>
        </div>
      )}
      {error && <p className="field-error">{error}</p>}
    </article>
  )
}
