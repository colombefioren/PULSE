import * as Dialog from '@radix-ui/react-dialog'
import * as Tooltip from '@radix-ui/react-tooltip'
import { useCallback, useState } from 'react'
import { Outlet, useNavigate } from 'react-router'
import { Sidebar } from './Sidebar'
import { useGoShortcuts } from './shortcuts'
import { Topbar } from './Topbar'

const COLLAPSED_KEY = 'pulse-sidebar-collapsed'

function readCollapsed(): boolean {
  try {
    return localStorage.getItem(COLLAPSED_KEY) === '1'
  } catch {
    return false
  }
}

export function AppShell() {
  const navigate = useNavigate()
  const [collapsed, setCollapsed] = useState(readCollapsed)
  const [drawerOpen, setDrawerOpen] = useState(false)

  useGoShortcuts(useCallback((to: string) => void navigate(to), [navigate]))

  const toggleCollapsed = () => {
    const next = !collapsed
    try {
      localStorage.setItem(COLLAPSED_KEY, next ? '1' : '0')
    } catch {
      // The preference simply won't persist.
    }
    setCollapsed(next)
  }

  return (
    <Tooltip.Provider>
      <a
        href="#content"
        className="sr-only focus:not-sr-only focus:fixed focus:left-4 focus:top-4 focus:z-50 focus:rounded-control focus:bg-brand focus:px-3 focus:py-2 focus:text-brand-ink"
      >
        Skip to content
      </a>

      <div className="flex min-h-dvh">
        <aside
          className={`sticky top-0 hidden h-dvh shrink-0 border-r border-border bg-surface md:block ${
            collapsed ? 'w-14' : 'w-56'
          }`}
        >
          <Sidebar collapsed={collapsed} onToggleCollapsed={toggleCollapsed} />
        </aside>

        <Dialog.Root open={drawerOpen} onOpenChange={setDrawerOpen}>
          <Dialog.Portal>
            <Dialog.Overlay className="fixed inset-0 z-40 bg-black/40 md:hidden" />
            <Dialog.Content
              aria-describedby={undefined}
              className="fixed inset-y-0 left-0 z-50 w-64 border-r border-border bg-surface shadow-overlay md:hidden"
            >
              <Dialog.Title className="sr-only">Navigation</Dialog.Title>
              <Sidebar onNavigate={() => setDrawerOpen(false)} />
            </Dialog.Content>
          </Dialog.Portal>
        </Dialog.Root>

        <div className="flex min-w-0 flex-1 flex-col">
          <Topbar onOpenNav={() => setDrawerOpen(true)} />
          <main id="content" tabIndex={-1} className="flex-1 px-4 py-6 md:px-8">
            <div className="max-w-5xl">
              <Outlet />
            </div>
          </main>
        </div>
      </div>
    </Tooltip.Provider>
  )
}
