import { useSyncExternalStore } from 'react'

/**
 * "Try the demo" needs no account: it sets a flag for this tab and the app shows the seeded workspace.
 * The flag lives in sessionStorage so it ends with the tab and never outlives a real sign-in.
 */
const KEY = 'pulse-demo'
const listeners = new Set<() => void>()

function read(): boolean {
  try {
    return sessionStorage.getItem(KEY) === '1'
  } catch {
    return false
  }
}

function write(on: boolean) {
  try {
    if (on) sessionStorage.setItem(KEY, '1')
    else sessionStorage.removeItem(KEY)
  } catch {
    // Storage can be blocked (private windows); the demo then lasts until the page reloads.
    memory = on
  }
  listeners.forEach((listener) => listener())
}

let memory = false

export function isDemoMode(): boolean {
  return read() || memory
}

export function enterDemo() {
  write(true)
}

export function leaveDemo() {
  memory = false
  write(false)
}

export function useDemoMode(): boolean {
  return useSyncExternalStore(
    (listener) => {
      listeners.add(listener)
      return () => listeners.delete(listener)
    },
    isDemoMode,
    () => false,
  )
}
