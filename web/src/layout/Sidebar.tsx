import { PanelLeftClose, PanelLeftOpen } from 'lucide-react'
import { NavLink } from 'react-router'
import { Logo, LogoMark } from '@/components/Logo'
import { Tip } from '@/components/Tip'
import { NAV_ITEMS } from './nav'

interface SidebarProps {
  collapsed?: boolean
  onToggleCollapsed?: () => void
  /** Called after a link is followed, so the mobile drawer can close. */
  onNavigate?: () => void
}

export function Sidebar({ collapsed = false, onToggleCollapsed, onNavigate }: SidebarProps) {
  return (
    <div className="flex h-full flex-col">
      <div className={`flex h-12 items-center border-b border-border ${collapsed ? 'justify-center' : 'px-4'}`}>
        {collapsed ? <LogoMark height={24} animate /> : <Logo height={22} />}
      </div>

      <nav aria-label="Main" className="flex-1 p-2">
        <ul className="flex flex-col gap-0.5">
          {NAV_ITEMS.map(({ to, label, icon: Icon, goKey }) => (
            <li key={to}>
              <Tip label={label} keys={goKey ? ['G', goKey.toUpperCase()] : undefined} side="right">
                <NavLink
                  to={to}
                  onClick={onNavigate}
                  aria-label={collapsed ? label : undefined}
                  className={({ isActive }) =>
                    [
                      'flex h-8 items-center gap-3 rounded-control text-sm font-medium',
                      collapsed ? 'justify-center' : 'px-2',
                      isActive ? 'bg-brand text-brand-ink' : 'text-muted hover:bg-surface-hover hover:text-fg',
                    ].join(' ')
                  }
                >
                  <Icon size={16} strokeWidth={1.5} aria-hidden />
                  {!collapsed && <span>{label}</span>}
                </NavLink>
              </Tip>
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
