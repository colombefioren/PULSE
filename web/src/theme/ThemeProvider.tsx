import { useCallback, useEffect, useMemo, useState, useSyncExternalStore, type ReactNode } from 'react'
import { nextPreference, readStoredPreference, resolveTheme, storePreference, type ThemePreference } from './theme'
import { ThemeContext } from './theme-context'

const DARK_QUERY = '(prefers-color-scheme: dark)'

function subscribeToSystemTheme(onChange: () => void) {
  const query = matchMedia(DARK_QUERY)
  query.addEventListener('change', onChange)
  return () => query.removeEventListener('change', onChange)
}

const systemPrefersDark = () => matchMedia(DARK_QUERY).matches

export function ThemeProvider({ children }: { children: ReactNode }) {
  const [preference, setPreferenceState] = useState<ThemePreference>(readStoredPreference)
  const systemDark = useSyncExternalStore(subscribeToSystemTheme, systemPrefersDark, () => false)
  const resolved = resolveTheme(preference, systemDark)

  useEffect(() => {
    document.documentElement.dataset.theme = resolved
  }, [resolved])

  const setPreference = useCallback((next: ThemePreference) => {
    storePreference(next)
    setPreferenceState(next)
  }, [])

  const cycle = useCallback(() => setPreference(nextPreference(preference)), [preference, setPreference])

  const value = useMemo(
    () => ({ preference, resolved, setPreference, cycle }),
    [preference, resolved, setPreference, cycle],
  )

  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>
}
