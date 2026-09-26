import { useEffect, useState, type FormEvent } from 'react'
import { api, ApiError, departmentLabel, formatDate, slaLabel, type Status, type TrackResult } from '../api'

const steps: Status[] = ['Open', 'Active', 'Finalized']

export default function TrackPage() {
  const initial = new URLSearchParams(window.location.search).get('id') ?? ''
  const [id, setId] = useState(initial)
  const [result, setResult] = useState<TrackResult | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function lookup(requestId: string) {
    if (!requestId.trim()) return
    setBusy(true)
    setError(null)
    setResult(null)
    try {
      setResult(await api.track(requestId))
      window.history.replaceState({}, '', `/track?id=${encodeURIComponent(requestId.trim())}`)
    } catch (err) {
      setError(err instanceof ApiError && err.status === 404 ? 'No request found with that ID.' : 'Could not look up the request.')
    } finally {
      setBusy(false)
    }
  }

  useEffect(() => {
    if (initial) lookup(initial)
  }, [])

  const onSubmit = (e: FormEvent) => {
    e.preventDefault()
    lookup(id)
  }

  const current = result ? steps.indexOf(result.status) : -1
  const sla = result ? slaLabel(result.slaDueAt) : null

  return (
    <section className="card narrow">
      <h1>Track a request</h1>
      <form onSubmit={onSubmit} className="inline-form">
        <input value={id} onChange={(e) => setId(e.target.value)} placeholder="e.g. REQ-IT-336967343854" aria-label="Request ID" />
        <button className="button" disabled={busy || !id.trim()}>
          {busy ? 'Looking up…' : 'Look up'}
        </button>
      </form>
      {error && <p className="alert">{error}</p>}
      {result && (
        <div className="track-result">
          <p className="eyebrow">{result.requestId}</p>
          <h2>{result.subject}</h2>
          <ol className="steps">
            {steps.map((s, i) => (
              <li key={s} className={i < current ? 'done' : i === current ? 'current' : ''}>
                <span className="dot" />
                {s}
              </li>
            ))}
          </ol>
          <dl className="facts">
            <dt>Department</dt>
            <dd>{departmentLabel[result.department] ?? result.department}</dd>
            <dt>Priority</dt>
            <dd>
              <span className={`badge prio-${result.priority.toLowerCase()}`}>{result.priority}</span>
              {result.escalated && <span className="badge danger">Escalated</span>}
            </dd>
            <dt>Submitted</dt>
            <dd>{formatDate(result.createdAt)}</dd>
            {result.status !== 'Finalized' && (
              <>
                <dt>Target resolution</dt>
                <dd className={sla?.overdue ? 'overdue' : ''}>
                  {formatDate(result.slaDueAt)} ({sla?.text})
                </dd>
              </>
            )}
            {result.resolutionNote && (
              <>
                <dt>Resolution</dt>
                <dd>{result.resolutionNote}</dd>
              </>
            )}
          </dl>
        </div>
      )}
    </section>
  )
}
