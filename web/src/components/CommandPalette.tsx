import * as Dialog from '@radix-ui/react-dialog'
import { Activity, ArrowRight, Moon, Plus, Search, Sun, Users } from 'lucide-react'
import { useId, useMemo, useState, type KeyboardEvent } from 'react'
import { useNavigate } from 'react-router'
import { useMonitorAccess, useMonitorList } from '@/data/monitors'
import { NAV_ITEMS } from '@/layout/nav'
import { NEW_MONITOR_PATH } from '@/layout/shortcuts'
import { useSession } from '@/session/useSession'
import { useTheme } from '@/theme/useTheme'
import { filterCommands, GROUP_ORDER, type Command } from './commands'

interface CommandPaletteProps {
  open: boolean
  onOpenChange: (open: boolean) => void
}

function Palette({ onClose }: { onClose: () => void }) {
  const navigate = useNavigate()
  const session = useSession()
  const { canEdit } = useMonitorAccess()
  const monitors = useMonitorList()
  const { resolved, setPreference } = useTheme()
  const [query, setQuery] = useState('')
  const [active, setActive] = useState(0)
  const listId = useId()

  const commands = useMemo<Command[]>(() => {
    const go = (to: string) => () => {
      onClose()
      void navigate(to)
    }
    const list: Command[] = NAV_ITEMS.map((item) => ({
      id: `go:${item.to}`,
      group: 'Go to',
      label: item.label,
      icon: item.icon,
      run: go(item.to),
    }))

    if (canEdit) {
      list.push({ id: 'create-monitor', group: 'Actions', label: 'Create monitor', keywords: 'add new', icon: Plus, run: go(NEW_MONITOR_PATH) })
    }
    if (session.status === 'ready' && session.mode === 'user') {
      for (const org of session.orgs) {
        if (org.id === session.org?.id) continue
        list.push({
          id: `team:${org.id}`,
          group: 'Actions',
          label: `Switch to ${org.name}`,
          keywords: 'team organization workspace',
          icon: Users,
          run: () => {
            session.selectOrg(org.id)
            onClose()
            void navigate('/dashboard')
          },
        })
      }
    }
    list.push({
      id: 'toggle-theme',
      group: 'Actions',
      label: resolved === 'dark' ? 'Switch to light theme' : 'Switch to dark theme',
      keywords: 'toggle theme appearance mode dark light',
      icon: resolved === 'dark' ? Sun : Moon,
      run: () => {
        setPreference(resolved === 'dark' ? 'light' : 'dark')
        onClose()
      },
    })

    for (const monitor of monitors.data ?? []) {
      list.push({ id: `monitor:${monitor.id}`, group: 'Monitors', label: monitor.name, hint: monitor.url, icon: Activity, run: go(`/monitors/${monitor.id}`) })
    }
    return list
  }, [canEdit, monitors.data, navigate, onClose, resolved, session, setPreference])

  const results = useMemo(() => filterCommands(commands, query), [commands, query])
  const current = Math.min(active, Math.max(results.length - 1, 0))
  const optionId = (index: number) => `${listId}-option-${index}`

  const onKeyDown = (event: KeyboardEvent) => {
    if (event.key === 'ArrowDown') {
      event.preventDefault()
      setActive(results.length === 0 ? 0 : (current + 1) % results.length)
    } else if (event.key === 'ArrowUp') {
      event.preventDefault()
      setActive(results.length === 0 ? 0 : (current - 1 + results.length) % results.length)
    } else if (event.key === 'Enter') {
      event.preventDefault()
      results[current]?.run()
    }
  }

  return (
    <>
      <div className="flex items-center gap-3 border-b border-border px-4">
        <Search size={16} strokeWidth={1.5} aria-hidden className="shrink-0 text-muted" />
        <input
          autoFocus
          role="combobox"
          aria-expanded
          aria-controls={listId}
          aria-activedescendant={results.length > 0 ? optionId(current) : undefined}
          aria-label="Search monitors and commands"
          autoComplete="off"
          spellCheck={false}
          placeholder="Search monitors, pages and actions"
          className="h-14 min-w-0 flex-1 bg-transparent text-base outline-none placeholder:text-subtle"
          value={query}
          onChange={(event) => {
            setQuery(event.target.value)
            setActive(0)
          }}
          onKeyDown={onKeyDown}
        />
      </div>

      <div id={listId} role="listbox" aria-label="Results" className="max-h-[min(24rem,60dvh)] overflow-y-auto p-2">
        {results.length === 0 ? (
          <p className="px-3 py-6 text-center text-sm text-muted">
            {monitors.isPending && query ? 'Loading monitors…' : `Nothing matches “${query}”.`}
          </p>
        ) : (
          GROUP_ORDER.map((group) => {
            const inGroup = results.filter((command) => command.group === group)
            if (inGroup.length === 0) return null
            return (
              <div key={group} role="group" aria-label={group} className="mb-1">
                <div aria-hidden className="px-3 pb-1 pt-2 text-xs font-medium uppercase tracking-wider text-muted">
                  {group}
                </div>
                {inGroup.map((command) => {
                  const index = results.indexOf(command)
                  const Icon = command.icon
                  const selected = index === current
                  return (
                    <div
                      key={command.id}
                      id={optionId(index)}
                      role="option"
                      aria-selected={selected}
                      onMouseMove={() => setActive(index)}
                      onClick={command.run}
                      className={`flex min-h-11 cursor-pointer items-center gap-3 rounded-xl px-3 py-2 text-base ${selected ? 'bg-surface-2' : ''}`}
                    >
                      <Icon size={16} strokeWidth={1.5} aria-hidden className="shrink-0 text-muted" />
                      <span className="min-w-0 flex-1 truncate">{command.label}</span>
                      {command.hint && <span className="mono hidden max-w-[45%] truncate text-xs text-muted sm:block">{command.hint}</span>}
                      {selected && <ArrowRight size={16} strokeWidth={1.5} aria-hidden className="shrink-0 text-muted" />}
                    </div>
                  )
                })}
              </div>
            )
          })
        )}
      </div>

      <p className="border-t border-border px-4 py-2 text-xs text-muted">
        <kbd className="mono">Up</kbd> <kbd className="mono">Down</kbd> to move, <kbd className="mono">Enter</kbd> to open, <kbd className="mono">Esc</kbd> to close
      </p>
    </>
  )
}

/** Cmd/Ctrl+K: jump to any monitor or page, create a monitor, switch team, change theme. */
export function CommandPalette({ open, onOpenChange }: CommandPaletteProps) {
  return (
    <Dialog.Root open={open} onOpenChange={onOpenChange}>
      <Dialog.Portal>
        <Dialog.Overlay className="fixed inset-0 z-50 bg-black/50" />
        <Dialog.Content
          aria-describedby={undefined}
          className="fixed left-1/2 top-[12dvh] z-50 w-[calc(100vw-2rem)] max-w-xl -translate-x-1/2 overflow-hidden rounded-shell bg-bg text-fg shadow-overlay"
        >
          <Dialog.Title className="sr-only">Command palette</Dialog.Title>
          <Palette onClose={() => onOpenChange(false)} />
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  )
}
