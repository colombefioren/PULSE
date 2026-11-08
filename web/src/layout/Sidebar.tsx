import * as DropdownMenu from '@radix-ui/react-dropdown-menu'
import { Check, ChevronsUpDown, LogOut, PanelLeftClose, PanelLeftOpen, Plus } from 'lucide-react'
import { useState } from 'react'
import { NavLink, useMatch, useNavigate } from 'react-router'
import { useLogout } from '@/api/hooks'
import { CreateTeamDialog } from '@/components/CreateTeamDialog'
import { Logo, LogoMark } from '@/components/Logo'
import { Tip } from '@/components/Tip'
import { leaveDemo } from '@/session/demoMode'
import { useSession } from '@/session/useSession'
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

const MENU_ITEM =
  'flex cursor-pointer items-center gap-2 rounded-xl px-3 py-2 text-sm text-panel-fg outline-none data-[highlighted]:bg-panel'

/** The chip at the top of the sidebar. For a real account it opens a menu to switch team or create one. */
function TeamSwitcher() {
  const session = useSession()
  const [creating, setCreating] = useState(false)

  if (session.status !== 'ready') return null
  const { user, org, orgs, mode } = session

  const chip = (
    <>
      <span
        aria-hidden
        className="flex size-9 shrink-0 items-center justify-center rounded-full bg-brand text-sm font-semibold text-brand-ink"
      >
        {user.initials}
      </span>
      <span className="min-w-0 flex-1 text-left leading-tight">
        <span className="block truncate text-sm font-medium">{org?.name ?? 'No team yet'}</span>
        <span className="block truncate text-xs text-panel-muted">
          {user.name}
          {org ? ` · ${org.role}` : ''}
        </span>
      </span>
    </>
  )

  if (mode === 'demo') {
    return <div className="mx-3 mb-4 flex items-center gap-3 rounded-2xl bg-panel-2 p-3">{chip}</div>
  }

  return (
    <>
      <DropdownMenu.Root>
        <DropdownMenu.Trigger
          aria-label={`Team: ${org?.name ?? 'none'}. Switch team`}
          className="mx-3 mb-4 flex w-[calc(100%-1.5rem)] items-center gap-3 rounded-2xl bg-panel-2 p-3 text-panel-fg outline-none transition-colors hover:bg-panel-2/80 focus-visible:ring-2 focus-visible:ring-brand"
        >
          {chip}
          <ChevronsUpDown size={16} strokeWidth={1.5} aria-hidden className="shrink-0 text-panel-muted" />
        </DropdownMenu.Trigger>
        <DropdownMenu.Portal>
          <DropdownMenu.Content
            align="start"
            sideOffset={6}
            className="z-50 min-w-60 rounded-2xl bg-panel-2 p-2 text-panel-fg shadow-overlay"
          >
            <DropdownMenu.Label className="px-3 py-2 text-xs text-panel-muted">{user.email}</DropdownMenu.Label>
            {orgs.map((team) => (
              <DropdownMenu.Item key={team.id} className={MENU_ITEM} onSelect={() => session.selectOrg(team.id)}>
                <span className="min-w-0 flex-1 truncate">{team.name}</span>
                <span className="text-xs text-panel-muted">{team.role}</span>
                {team.id === org?.id && <Check size={16} strokeWidth={1.5} aria-hidden />}
              </DropdownMenu.Item>
            ))}
            <DropdownMenu.Separator className="my-2 h-px bg-white/10" />
            <DropdownMenu.Item className={MENU_ITEM} onSelect={() => setCreating(true)}>
              <Plus size={16} strokeWidth={1.5} aria-hidden />
              New team
            </DropdownMenu.Item>
          </DropdownMenu.Content>
        </DropdownMenu.Portal>
      </DropdownMenu.Root>
      <CreateTeamDialog open={creating} onOpenChange={setCreating} />
    </>
  )
}

export function Sidebar({ collapsed = false, onToggleCollapsed, onNavigate }: SidebarProps) {
  const session = useSession()
  const navigate = useNavigate()
  const logout = useLogout()

  const signOut = () => {
    onNavigate?.()
    if (session.status === 'ready' && session.mode === 'demo') {
      leaveDemo()
      void navigate('/')
      return
    }
    logout.mutate(undefined, { onSettled: () => void navigate('/') })
  }

  return (
    <div className="flex h-full flex-col text-panel-fg">
      <div className={`flex h-16 items-center ${collapsed ? 'justify-center' : 'px-6'}`}>
        {collapsed ? <LogoMark height={26} animate /> : <Logo height={24} iconGap={10} />}
      </div>

      {!collapsed && <TeamSwitcher />}

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
        <Tip label="Log out" side="right">
          <button
            type="button"
            onClick={signOut}
            aria-label="Log out"
            className="flex h-10 items-center gap-3 rounded-full px-3 text-sm text-panel-muted hover:bg-panel-2 hover:text-panel-fg"
          >
            <LogOut size={16} strokeWidth={1.5} aria-hidden />
            {!collapsed && <span>Log out</span>}
          </button>
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
