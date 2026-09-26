import { useEffect, useState, type MouseEvent, type ReactNode } from 'react'
import SubmitPage from './pages/SubmitPage'
import BoardPage from './pages/BoardPage'
import TrackPage from './pages/TrackPage'

// Three pages don't need a router library: map the path to a page and use pushState for navigation.
const routes: Record<string, () => ReactNode> = {
  '/': () => <SubmitPage />,
  '/board': () => <BoardPage />,
  '/track': () => <TrackPage />,
}

export function navigate(to: string) {
  window.history.pushState({}, '', to)
  window.dispatchEvent(new PopStateEvent('popstate'))
}

export function Link({ to, children, className }: { to: string; children: ReactNode; className?: string }) {
  const onClick = (e: MouseEvent) => {
    if (e.metaKey || e.ctrlKey) return
    e.preventDefault()
    navigate(to)
  }
  return (
    <a href={to} onClick={onClick} className={className}>
      {children}
    </a>
  )
}

export default function App() {
  const [path, setPath] = useState(window.location.pathname)

  useEffect(() => {
    const onPop = () => setPath(window.location.pathname)
    window.addEventListener('popstate', onPop)
    return () => window.removeEventListener('popstate', onPop)
  }, [])

  const page = routes[path] ?? routes['/']
  const nav = [
    { to: '/', label: 'Submit request' },
    { to: '/track', label: 'Track request' },
    { to: '/board', label: 'Agent board' },
  ]

  return (
    <div className="shell">
      <header className="topbar">
        <Link to="/" className="brand">
          <span className="brand-mark">ER</span>
          Employee Requests
        </Link>
        <nav>
          {nav.map((n) => (
            <Link key={n.to} to={n.to} className={path === n.to ? 'active' : ''}>
              {n.label}
            </Link>
          ))}
        </nav>
      </header>
      <main>{page()}</main>
      <footer className="footer">
        Proof of concept · Web app (ASP.NET Core + React) → HubSpot CRM tickets → Zapier automations
      </footer>
    </div>
  )
}
