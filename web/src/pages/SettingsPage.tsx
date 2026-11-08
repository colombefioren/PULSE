import { PageHeader } from '@/components/PageHeader'
import type { ThemePreference } from '@/theme/theme'
import { useTheme } from '@/theme/useTheme'

const OPTIONS: { value: ThemePreference; label: string }[] = [
  { value: 'system', label: 'System' },
  { value: 'light', label: 'Light' },
  { value: 'dark', label: 'Dark' },
]

export default function SettingsPage() {
  const { preference, setPreference } = useTheme()

  return (
    <>
      <PageHeader title="Settings" />
      <section aria-labelledby="appearance" className="card max-w-xl p-6">
        <h2 id="appearance" className="text-md text-fg">
          Appearance
        </h2>
        <p className="mt-1 text-base text-muted">System follows your operating system setting.</p>
        <div role="radiogroup" aria-labelledby="appearance" className="mt-4 flex gap-2">
          {OPTIONS.map(({ value, label }) => (
            <button
              key={value}
              type="button"
              role="radio"
              aria-checked={preference === value}
              onClick={() => setPreference(value)}
              className={`btn ${preference === value ? 'btn-primary' : ''}`}
            >
              {label}
            </button>
          ))}
        </div>
      </section>
    </>
  )
}
