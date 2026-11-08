import { PanelLeftClose, PanelLeftOpen } from 'lucide-react'
import { NavLink, useMatch } from 'react-router'
import { Logo, LogoMark } from '@/components/Logo'
import { Tip } from '@/components/Tip'
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
          'flex h-8 items-center gap-3 rounded-control text-sm font-medium',
          collapsed ? 'justify-center' : 'px-2',
          isActive ? 'bg-brand text-brand-ink' : 'text-muted hover:bg-surface-hover hover:text-fg',
        ].join(' ')}
      >
        <Icon size={16} strokeWidth={1.5} aria-hidden />
        {!collapsed && <span>{label}</span>}
      </NavLink>
    </Tip>
  )
}

export function Sidebar({ collapsed = false, onToggleCollapsed, onNavigate }: SidebarProps) {
  return (
    <div className="flex h-full flex-col">
      <div className={`flex h-12 items-center border-b border-border ${collapsed ? 'justify-center' : 'px-4'}`}>
        {collapsed ? <LogoMark height={24} animate /> : <Logo height={22} />}
      </div>

      <nav aria-label="Main" className="flex-1 p-2">
        <ul className="flex flex-col gap-0.5">
          {NAV_ITEMS.map((item) => (
            <li key={item.to}>
              <SidebarLink item={item} collapsed={collapsed} onNavigate={onNavigate} />
            </li>
          ))}
        </ul>
      </nav>

      {onToggleCollapsed && (
        <div className={`border-t border-border p-2 ${collapsed ? 'flex justify-center' : ''}`}>
          <Tip label={collapsed ? 'Expand sidebar' : 'Collapse sidebar'} side="right">
            <button
              type="button"
              className="btn btn-ghost btn-icon"
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
        </div>
      )}
    </div>
  )
}
