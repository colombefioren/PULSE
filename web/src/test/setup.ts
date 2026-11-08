import '@testing-library/jest-dom/vitest'
import { cleanup, configure } from '@testing-library/react'
import { afterEach, vi } from 'vitest'
import { demo } from '@/data/demo'
import { leaveDemo } from '@/session/demoMode'

// The first screen of a file compiles its lazy route on demand, which can take over a second on a busy machine.
configure({ asyncUtilTimeout: 5000 })

// Tests never open a real socket; the live layer is exercised through its pure `apply` functions.
vi.mock('@/live/connection', () => ({ connectLive: vi.fn().mockResolvedValue({ stop: vi.fn() }) }))
vi.mock('@/live/status', () => ({ connectStatus: vi.fn().mockResolvedValue({ stop: vi.fn() }) }))

// jsdom has no matchMedia; tests start from a light system theme.
Object.defineProperty(window, 'matchMedia', {
  writable: true,
  value: vi.fn().mockImplementation((query: string) => ({
    matches: false,
    media: query,
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
    addListener: vi.fn(),
    removeListener: vi.fn(),
    dispatchEvent: vi.fn(),
    onchange: null,
  })),
})

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
  localStorage.clear()
  sessionStorage.clear()
  leaveDemo()
  demo.reset()
  delete document.documentElement.dataset.theme
})
