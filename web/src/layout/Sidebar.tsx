import { LogOut, PanelLeftClose, PanelLeftOpen } from 'lucide-react'
import { Link, NavLink, useMatch } from 'react-router'
import { Logo, LogoMark } from '@/components/Logo'
import { Tip } from '@/components/Tip'
import { demoSession } from '@/data/session'
import { NAV_ITEMS, type NavItem } from './nav'

interface SidebarProps {
  collapsed?: boolean
  onToggleCollapsed?: () => void
  /** Called after a link is followed, so the mobile drawer can close. */
  onNavigate?: () => void
}

interface SidebarLinkProps {
  item: NavItem
  collapsed: boolean
  onNavigate?: () => void
}

/**
 * The active state is computed here and passed as a plain string: the tooltip trigger merges
 * class names as strings, which would break React Router's function-style className.
 */
function SidebarLink({ item: { to, label, icon: Icon, goKey }, collapsed, onNavigate }: SidebarLinkProps) {
  const isActive = useMatch({ path: to, end: false }) !== null

  return (
    <Tip label={label} keys={goKey ? ['G', goKey.toUpperCase()] : undefined} side="right">
      <NavLink
        to={to}
        onClick={onNavigate}
        aria-label={collapsed ? label : undefined}
        className={[
          'flex h-10 items-center gap-3 rounded-full text-sm font-medium transition-colors',
          collapsed ? 'justify-center' : 'px-4',
          isActive ? 'bg-bg text-fg' : 'text-panel-muted hover:bg-panel-2 hover:text-panel-fg',
        ].join(' ')}
      >
        <Icon size={16} strokeWidth={1.5} aria-hidden />
        {!collapsed && <span>{label}</span>}
      </NavLink>
    </Tip>
  )
}

export function Sidebar({ collapsed = false, onToggleCollapsed, onNavigate }: SidebarProps) {
  const { user, org } = demoSession

  return (
    <div className="flex h-full flex-col text-panel-fg">
      <div className={`flex h-16 items-center ${collapsed ? 'justify-center' : 'px-6'}`}>
        {collapsed ? <LogoMark height={26} animate /> : <Logo height={24} iconGap={10} />}
      </div>

      {!collapsed && (
        <div className="mx-3 mb-4 flex items-center gap-3 rounded-2xl bg-panel-2 p-3">
          <span
            aria-hidden
            className="flex size-9 shrink-0 items-center justify-center rounded-full bg-brand text-sm font-semibold text-brand-ink"
          >
            {user.initials}
          </span>
          <span className="min-w-0 leading-tight">
            <span className="block truncate text-sm font-medium">{org.name}</span>
            <span className="block truncate text-xs text-panel-muted">
              {user.name} · {org.role}
            </span>
          </span>
        </div>
      )}

      <nav aria-label="Main" className="flex-1 px-3">
        <ul className="flex flex-col gap-1">
          {NAV_ITEMS.map((item) => (
            <li key={item.to}>
              <SidebarLink item={item} collapsed={collapsed} onNavigate={onNavigate} />
            </li>
          ))}
        </ul>
      </nav>

      <div className={`flex items-center gap-1 p-3 ${collapsed ? 'flex-col' : 'justify-between'}`}>
        <Tip label="Back to the site" side="right">
          <Link
            to="/"
            onClick={onNavigate}
            aria-label="Back to the site"
            className="flex h-10 items-center gap-3 rounded-full px-3 text-sm text-panel-muted hover:bg-panel-2 hover:text-panel-fg"
          >
            <LogOut size={16} strokeWidth={1.5} aria-hidden />
            {!collapsed && <span>Log out</span>}
          </Link>
        </Tip>

        {onToggleCollapsed && (
          <Tip label={collapsed ? 'Expand sidebar' : 'Collapse sidebar'} side="right">
            <button
              type="button"
              className="flex size-10 items-center justify-center rounded-full text-panel-muted hover:bg-panel-2 hover:text-panel-fg"
              onClick={onToggleCollapsed}
              aria-label={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}
              aria-expanded={!collapsed}
            >
              {collapsed ? (
                <PanelLeftOpen size={16} strokeWidth={1.5} aria-hidden />
              ) : (
                <PanelLeftClose size={16} strokeWidth={1.5} aria-hidden />
              )}
            </button>
          </Tip>
        )}
      </div>
    </div>
  )
}
