// Typed wrappers for the ASP.NET Core API. All data lives in HubSpot; the API is the only thing that talks to it.

export type Status = 'Open' | 'Active' | 'Finalized'
export type Priority = 'Low' | 'Medium' | 'High' | 'Urgent'

export interface SubmitPayload {
  name: string
  email: string
  subject: string
  description: string
  urgency: 'Low' | 'Medium' | 'High'
}

export interface SubmitResult {
  requestId: string
  ticketId: string
  department: string
  priority: Priority
  slaDueAt: string
  classifiedBy: string
  confidence: number
  sourceChannel: string
}

export interface TrackResult {
  requestId: string
  subject: string
  department: string
  priority: Priority
  status: Status
  slaDueAt: string | null
  escalated: boolean
  resolutionNote: string | null
  createdAt: string
}

export interface BoardTicket extends TrackResult {
  id: string
  description: string | null
  employeeName: string | null
  classifiedBy: string | null
  confidence: number | null
  sourceChannel: string | null
}

export class ApiError extends Error {
  status: number
  fieldErrors: Record<string, string[]>

  constructor(message: string, status: number, fieldErrors: Record<string, string[]> = {}) {
    super(message)
    this.status = status
    this.fieldErrors = fieldErrors
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(path, {
    ...init,
    headers: { 'Content-Type': 'application/json', ...init?.headers },
  })
  const body = res.status === 204 ? null : await res.json().catch(() => null)
  if (!res.ok) {
    const message = body?.error ?? body?.title ?? (res.status === 404 ? 'Not found' : `Request failed (${res.status})`)
    throw new ApiError(message, res.status, body?.errors ?? {})
  }
  return body as T
}

export const api = {
  submit: (payload: SubmitPayload) =>
    request<SubmitResult>('/api/requests', { method: 'POST', body: JSON.stringify(payload) }),
  track: (requestId: string) => request<TrackResult>(`/api/requests/${encodeURIComponent(requestId.trim())}`),
  tickets: () => request<BoardTicket[]>('/api/tickets'),
  changeStatus: (id: string, status: Status, resolutionNote?: string) =>
    request<BoardTicket>(`/api/tickets/${id}/status`, {
      method: 'PATCH',
      body: JSON.stringify({ status, resolutionNote }),
    }),
}

export const departmentLabel: Record<string, string> = {
  hr: 'HR',
  it: 'IT',
  payroll: 'Payroll',
  operations: 'Operations',
  other: 'Other',
}

export function formatDate(iso: string | null | undefined): string {
  if (!iso) return '—'
  return new Date(iso).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })
}

/** "in 12 min" / "3 h overdue" relative to now. */
export function slaLabel(iso: string | null, now = Date.now()): { text: string; overdue: boolean } {
  if (!iso) return { text: 'No SLA', overdue: false }
  const diffMin = Math.round((new Date(iso).getTime() - now) / 60000)
  const abs = Math.abs(diffMin)
  const span = abs < 60 ? `${abs} min` : abs < 48 * 60 ? `${Math.round(abs / 60)} h` : `${Math.round(abs / 1440)} d`
  return diffMin >= 0 ? { text: `due in ${span}`, overdue: false } : { text: `${span} overdue`, overdue: true }
}
