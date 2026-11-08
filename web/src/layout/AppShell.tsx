import * as Dialog from '@radix-ui/react-dialog'
import * as Tooltip from '@radix-ui/react-tooltip'
import { useCallback, useEffect, useState } from 'react'
import { Outlet, useNavigate } from 'react-router'
import { CommandPalette } from '@/components/CommandPalette'
import { useLive } from '@/live/useLive'
import { DemoBanner } from './DemoBanner'
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
  const [paletteOpen, setPaletteOpen] = useState(false)

  useLive()
  useGoShortcuts(useCallback((to: string) => void navigate(to), [navigate]))

  // Ctrl+K (Cmd+K on a Mac) opens the palette from anywhere, even inside a text field.
  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if ((event.ctrlKey || event.metaKey) && !event.altKey && !event.shiftKey && event.key.toLowerCase() === 'k') {
        event.preventDefault()
        setPaletteOpen((open) => !open)
      }
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [])

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
        className="sr-only focus:not-sr-only focus:fixed focus:left-4 focus:top-4 focus:z-50 focus:rounded-full focus:bg-brand focus:px-4 focus:py-2 focus:text-brand-ink"
      >
        Skip to content
      </a>

      <div className="min-h-dvh bg-canvas md:p-4 lg:p-6">
        <div className="mx-auto flex min-h-dvh max-w-[1520px] gap-3 md:min-h-[calc(100dvh-2rem)] lg:min-h-[calc(100dvh-3rem)] lg:gap-4">
          <aside
            className={`hidden shrink-0 rounded-shell bg-panel transition-[width] duration-200 md:sticky md:top-6 md:block md:h-[calc(100dvh-3rem)] ${
              collapsed ? 'w-[76px]' : 'w-64'
            }`}
          >
            <Sidebar collapsed={collapsed} onToggleCollapsed={toggleCollapsed} />
          </aside>

          <Dialog.Root open={drawerOpen} onOpenChange={setDrawerOpen}>
            <Dialog.Portal>
              <Dialog.Overlay className="fixed inset-0 z-40 bg-black/50 md:hidden" />
              <Dialog.Content
                aria-describedby={undefined}
                className="fixed inset-y-2 left-2 z-50 w-72 rounded-shell bg-panel shadow-overlay md:hidden"
              >
                <Dialog.Title className="sr-only">Navigation</Dialog.Title>
                <Sidebar onNavigate={() => setDrawerOpen(false)} />
              </Dialog.Content>
            </Dialog.Portal>
          </Dialog.Root>

          <div className="flex min-w-0 flex-1 flex-col bg-bg md:rounded-shell">
            <Topbar onOpenNav={() => setDrawerOpen(true)} onOpenSearch={() => setPaletteOpen(true)} />
            <DemoBanner />
            <main id="content" tabIndex={-1} className="flex-1 px-4 pb-8 pt-2 md:px-8">
              <Outlet />
            </main>
          </div>
        </div>
      </div>
      <CommandPalette open={paletteOpen} onOpenChange={setPaletteOpen} />
    </Tooltip.Provider>
  )
}
