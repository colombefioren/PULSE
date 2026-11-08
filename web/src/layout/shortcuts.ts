import { useEffect } from 'react'
import { NAV_ITEMS } from './nav'

/** "C" opens the new monitor form (the page ignores it for people who cannot add monitors). */
export const NEW_MONITOR_PATH = '/monitors?new=1'

/** Max time between G and the second key. */
const SEQUENCE_WINDOW_MS = 1000

export function isTypingTarget(target: EventTarget | null): boolean {
  if (!(target instanceof HTMLElement)) return false
  return target.isContentEditable || ['INPUT', 'TEXTAREA', 'SELECT'].includes(target.tagName)
}

/** Resolves the destination for the key pressed after G, or undefined when it maps to nothing. */
export function destinationForGoKey(key: string): string | undefined {
  return NAV_ITEMS.find((item) => item.goKey === key.toLowerCase())?.to
}

/** "G then M" opens monitors, "G then I" opens incidents, "C" creates a monitor. Ignored while typing in a field. */
export function useGoShortcuts(navigate: (to: string) => void) {
  useEffect(() => {
    let armedAt = 0

    function onKeyDown(event: KeyboardEvent) {
      if (event.ctrlKey || event.metaKey || event.altKey || isTypingTarget(event.target)) return

      const key = event.key.toLowerCase()
      const armed = armedAt > 0 && event.timeStamp - armedAt <= SEQUENCE_WINDOW_MS

      if (armed) {
        armedAt = 0
        const destination = destinationForGoKey(key)
        if (destination) {
          event.preventDefault()
          navigate(destination)
          return
        }
      }

      if (key === 'g') armedAt = event.timeStamp
      if (key === 'c') {
        event.preventDefault()
        navigate(NEW_MONITOR_PATH)
      }
    }

    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [navigate])
}
