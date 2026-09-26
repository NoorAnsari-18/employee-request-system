import { useState, type FormEvent } from 'react'
import { api, ApiError, departmentLabel, formatDate, type SubmitPayload, type SubmitResult } from '../api'
import { Link } from '../App'

const empty: SubmitPayload = { name: '', email: '', subject: '', description: '', urgency: 'Medium' }

export default function SubmitPage() {
  const [form, setForm] = useState(empty)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({})
  const [result, setResult] = useState<SubmitResult | null>(null)

  const set = (field: keyof SubmitPayload) => (e: { target: { value: string } }) =>
    setForm({ ...form, [field]: e.target.value })

  async function onSubmit(e: FormEvent) {
    e.preventDefault()
    setBusy(true)
    setError(null)
    setFieldErrors({})
    try {
      setResult(await api.submit(form))
      setForm(empty)
    } catch (err) {
      if (err instanceof ApiError) {
        setFieldErrors(err.fieldErrors)
        setError(Object.keys(err.fieldErrors).length ? 'Please fix the highlighted fields.' : err.message)
      } else {
        setError('Could not reach the server. Please try again.')
      }
    } finally {
      setBusy(false)
    }
  }

  if (result) {
    return (
      <section className="card narrow">
        <p className="eyebrow">Request received</p>
        <h1 className="request-id">{result.requestId}</h1>
        <p className="muted">Keep this ID to check progress. A confirmation email is on its way.</p>
        <dl className="facts">
          <dt>Category</dt>
          <dd>{departmentLabel[result.department] ?? result.department}</dd>
          <dt>Assigned to</dt>
          <dd>{result.assignedTeam}</dd>
          <dt>Priority</dt>
          <dd>
            <span className={`badge prio-${result.priority.toLowerCase()}`}>{result.priority}</span>
          </dd>
          <dt>Target resolution</dt>
          <dd>{formatDate(result.slaDueAt)}</dd>
          <dt>Categorised by</dt>
          <dd>
            {result.classifiedBy === 'rules'
              ? `Keyword rules (${Math.round(result.confidence * 100)}% confidence)`
              : 'Needs manual triage (no clear category)'}
          </dd>
        </dl>
        <div className="actions">
          <Link to={`/track?id=${result.requestId}`} className="button">
            Track this request
          </Link>
          <button className="button secondary" onClick={() => setResult(null)}>
            Submit another
          </button>
        </div>
      </section>
    )
  }

  const err = (f: string) => fieldErrors[f]?.[0]

  return (
    <section className="card narrow">
      <h1>Submit a request</h1>
      <p className="muted">
        Leave, IT support, payroll, facilities or anything else. We categorise and route it automatically.
      </p>
      <form onSubmit={onSubmit} noValidate>
        <div className="row">
          <label>
            Your name
            <input value={form.name} onChange={set('name')} required maxLength={100} aria-invalid={!!err('name')} />
            {err('name') && <span className="field-error">{err('name')}</span>}
          </label>
          <label>
            Work email
            <input type="email" value={form.email} onChange={set('email')} required maxLength={254} aria-invalid={!!err('email')} />
            {err('email') && <span className="field-error">{err('email')}</span>}
          </label>
        </div>
        <label>
          Subject
          <input value={form.subject} onChange={set('subject')} required maxLength={200} placeholder="e.g. VPN not connecting" aria-invalid={!!err('subject')} />
          {err('subject') && <span className="field-error">{err('subject')}</span>}
        </label>
        <label>
          Description
          <textarea value={form.description} onChange={set('description')} required rows={5} maxLength={5000} placeholder="What do you need, and by when?" aria-invalid={!!err('description')} />
          {err('description') && <span className="field-error">{err('description')}</span>}
        </label>
        <label>
          How urgent is it?
          <select value={form.urgency} onChange={set('urgency')}>
            <option>Low</option>
            <option>Medium</option>
            <option>High</option>
          </select>
        </label>
        {error && <p className="alert">{error}</p>}
        <button className="button" disabled={busy}>
          {busy ? 'Submitting…' : 'Submit request'}
        </button>
      </form>
    </section>
  )
}
