import { createContext } from 'react'
import type { ResolvedTheme, ThemePreference } from './theme'

export interface ThemeContextValue {
  preference: ThemePreference
  resolved: ResolvedTheme
  setPreference: (preference: ThemePreference) => void
  cycle: () => void
}

export const ThemeContext = createContext<ThemeContextValue | null>(null)
