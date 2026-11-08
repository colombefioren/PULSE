import { Menu, Search } from 'lucide-react'
import { Link, useMatches } from 'react-router'
import { ThemeToggle } from '@/components/ThemeToggle'
import { Tip } from '@/components/Tip'

export interface RouteHandle {
  crumb?: string
}

interface TopbarProps {
  onOpenNav: () => void
  onOpenSearch: () => void
}

const isMac = typeof navigator !== 'undefined' && /Mac|iPhone|iPad/.test(navigator.platform)

export function Topbar({ onOpenNav, onOpenSearch }: TopbarProps) {
  const crumbs = useMatches().flatMap((match) => {
    const crumb = (match.handle as RouteHandle | undefined)?.crumb
    return crumb ? [{ crumb, pathname: match.pathname }] : []
  })

  return (
    <header className="flex h-16 items-center gap-2 px-4 md:px-8">
      <div className="md:hidden">
        <Tip label="Open navigation">
          <button type="button" className="btn btn-ghost btn-icon" onClick={onOpenNav} aria-label="Open navigation">
            <Menu size={16} strokeWidth={1.5} aria-hidden />
          </button>
        </Tip>
      </div>

      <nav aria-label="Breadcrumb" className="min-w-0 flex-1">
        <ol className="flex items-center gap-2 text-sm text-muted">
          <li>
            <Link to="/" className="hover:text-fg">
              Pulse
            </Link>
          </li>
          {crumbs.map(({ crumb, pathname }, index) => (
            <li key={pathname} className="flex items-center gap-2">
              <span aria-hidden className="text-border-strong">
                /
              </span>
              <span
                className={`truncate ${index === crumbs.length - 1 ? 'font-medium text-fg' : ''}`}
                aria-current={index === crumbs.length - 1 ? 'page' : undefined}
              >
                {crumb}
              </span>
            </li>
          ))}
        </ol>
      </nav>

      <button
        type="button"
        className="btn btn-ghost gap-2"
        onClick={onOpenSearch}
        aria-label="Search and commands"
        aria-keyshortcuts="Control+K Meta+K"
      >
        <Search size={16} strokeWidth={1.5} aria-hidden />
        <span className="hidden text-muted sm:inline">Search</span>
        <kbd className="mono hidden rounded-md border border-border px-1.5 text-xs text-muted sm:inline">{isMac ? '⌘K' : 'Ctrl K'}</kbd>
      </button>
      <ThemeToggle />
    </header>
  )
}
