import { Monitor, Moon, Sun } from 'lucide-react'
import { Tip } from './Tip'
import { nextPreference, type ThemePreference } from '@/theme/theme'
import { useTheme } from '@/theme/useTheme'

const LABELS: Record<ThemePreference, string> = { system: 'System', light: 'Light', dark: 'Dark' }
const ICONS = { system: Monitor, light: Sun, dark: Moon }

export function ThemeToggle() {
  const { preference, cycle } = useTheme()
  const Icon = ICONS[preference]
  const next = LABELS[nextPreference(preference)].toLowerCase()

  return (
    <Tip label={`Theme: ${LABELS[preference]}. Switch to ${next}`}>
      <button
        type="button"
        className="btn btn-ghost btn-icon"
        onClick={cycle}
        aria-label={`Theme: ${LABELS[preference]}. Switch to ${next}`}
      >
        <Icon size={16} strokeWidth={1.5} aria-hidden />
      </button>
    </Tip>
  )
}
