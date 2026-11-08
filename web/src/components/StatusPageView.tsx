import { Check, Mail } from 'lucide-react'
import { useMemo, useState, type FormEvent } from 'react'
import { LogoMark } from '@/components/Logo'
import { ResponseChart } from '@/components/charts/ResponseChart'
import { UptimeBars } from '@/components/charts/UptimeBars'
import { RangePicker } from '@/components/RangePicker'
import { SeverityBadge } from '@/components/SeverityBadge'
import { StatusPill } from '@/components/StatusPill'
import { useGlobalSeries, useNow } from '@/data/queries'
import type { ChartRange, Incident, IncidentStatus, StatusPage } from '@/data/types'
import { groupComponents, overallStatus, type OverallState } from '@/lib/overall'
import { formatDuration, summarize } from '@/lib/stats'

const BANNER: Record<OverallState, { dot: string; tint: string }> = {
  operational: { dot: 'up', tint: 'color-mix(in srgb, var(--status-up) 14%, var(--surface))' },
  degraded: { dot: 'degraded', tint: 'color-mix(in srgb, var(--status-degraded) 16%, var(--surface))' },
  partial: { dot: 'degraded', tint: 'color-mix(in srgb, var(--status-degraded) 16%, var(--surface))' },
  major: { dot: 'down', tint: 'color-mix(in srgb, var(--status-down) 14%, var(--surface))' },
  maintenance: { dot: 'maintenance', tint: 'color-mix(in srgb, var(--status-maintenance) 14%, var(--surface))' },
}

const STATUS_LABEL: Record<IncidentStatus, string> = {
  investigating: 'Investigating',
  identified: 'Identified',
  monitoring: 'Monitoring',
  resolved: 'Resolved',
}

const THIRTY_DAYS = 30 * 24 * 3600 * 1000

function Timeline({ incident }: { incident: Incident }) {
  return (
    <ol className="mt-4 flex flex-col gap-3 border-l-2 border-border pl-4">
      {[...incident.updates].reverse().map((update) => (
        <li key={update.at} className="relative">
          <span aria-hidden className="absolute -left-[21px] top-1.5 size-2 rounded-full bg-border-strong" />
          <p className="text-sm">
            <span className="font-semibold">{STATUS_LABEL[update.status]}</span>
            <span className="mono ml-2 text-xs text-muted">{new Date(update.at).toLocaleString('en-GB')}</span>
          </p>
          <p className="mt-0.5 text-base text-muted">{update.message}</p>
        </li>
      ))}
    </ol>
  )
}

function SubscribeForm({ title }: { title: string }) {
  const [email, setEmail] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [sent, setSent] = useState(false)

  const submit = (event: FormEvent) => {
    event.preventDefault()
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
      setError('Enter an email address like name@company.com')
      return
    }
    setError(null)
    setSent(true)
  }

  if (sent) {
    return (
      <p className="flex items-center gap-2 text-base" role="status">
        <Check size={16} strokeWidth={1.5} aria-hidden /> Check {email} to confirm. Updates for {title} will land there.
      </p>
    )
  }

  return (
    <form onSubmit={submit} noValidate className="flex flex-col gap-2 sm:flex-row sm:items-start">
      <div className="field flex-1">
        <label htmlFor="subscribe-email" className="sr-only">Email address</label>
        <input
          id="subscribe-email"
          type="email"
          className="input"
          placeholder="you@company.com"
          autoComplete="email"
          value={email}
          aria-invalid={error ? true : undefined}
          aria-describedby={error ? 'subscribe-error' : undefined}
          onChange={(event) => setEmail(event.target.value)}
        />
        {error && <p id="subscribe-error" className="field-error">{error}</p>}
      </div>
      <button type="submit" className="btn btn-dark btn-lg">
        <Mail size={16} strokeWidth={1.5} aria-hidden /> Subscribe to updates
      </button>
    </form>
  )
}

/** A customer-facing status page: banner, components with 90-day bars, incidents and response times. */
export function StatusPageView({ page, embedded = false }: { page: StatusPage; embedded?: boolean }) {
  const now = useNow(5000)
  const [range, setRange] = useState<ChartRange>('24h')
  const global = useGlobalSeries(range)

  const overall = overallStatus(page.components)
  const banner = BANNER[overall.state]
  const groups = useMemo(() => groupComponents(page.components), [page.components])
  const stats = useMemo(() => (global.data ? summarize(global.data) : null), [global.data])
  const recent = page.history.filter((incident) => now - incident.startedAt <= THIRTY_DAYS)

  return (
    <div className={embedded ? '' : 'mx-auto w-full max-w-3xl px-4 py-8 md:py-14'}>
      <header className="mb-8 flex items-center gap-3">
        <LogoMark height={34} label="" />
        <h1 className="text-xl font-semibold">{page.title}</h1>
      </header>

      <section
        aria-label="Current status"
        className="flex items-center gap-4 rounded-card p-6"
        style={{ background: banner.tint }}
      >
        <span className="status-dot size-3.5" data-status={banner.dot} data-live aria-hidden />
        <div>
          <h2 className="text-lg font-semibold">{overall.headline}</h2>
          <p className="text-sm text-muted">This page updates automatically.</p>
        </div>
      </section>

      {page.activeIncidents.length > 0 && (
        <section aria-label="Active incidents" className="mt-6 flex flex-col gap-4">
          {page.activeIncidents.map((incident) => (
            <article key={incident.id} className="card p-5">
              <div className="flex flex-wrap items-center gap-2">
                <SeverityBadge severity={incident.severity} />
                <span className="text-xs font-medium text-muted">{STATUS_LABEL[incident.status]}</span>
              </div>
              <h3 className="mt-2 text-md">{incident.title}</h3>
              <Timeline incident={incident} />
            </article>
          ))}
        </section>
      )}

      <section aria-label="Components" className="mt-8 flex flex-col gap-8">
        {groups.map(([group, components]) => (
          <div key={group}>
            <h2 className="mb-3 text-sm font-semibold uppercase tracking-wider text-muted">{group}</h2>
            <ul className="card divide-y divide-border">
              {components.map((component) => (
                <li key={component.id} className="p-5">
                  <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
                    <span className="font-medium">{component.name}</span>
                    <span className="flex items-center gap-4">
                      <span className="mono text-xs text-muted">{component.uptime90d.toFixed(2)}% uptime</span>
                      <StatusPill status={component.status} />
                    </span>
                  </div>
                  <UptimeBars days={component.days} subject={component.name} />
                  <div className="mono mt-2 flex justify-between text-xs text-subtle" aria-hidden>
                    <span>90 days ago</span>
                    <span>Today</span>
                  </div>
                </li>
              ))}
            </ul>
          </div>
        ))}
      </section>

      {page.showResponseTimes && (
        <section aria-label="Response times" className="card mt-8 p-5">
          <header className="mb-3 flex flex-wrap items-center justify-between gap-3">
            <h2 className="text-md">Response time</h2>
            <RangePicker value={range} onChange={setRange} label="Response time range" />
          </header>
          {stats && (
            <p className="mono mb-2 text-sm text-muted">
              Average <strong className="text-fg">{stats.avgMs} ms</strong> · p95 <strong className="text-fg">{stats.p95} ms</strong>
            </p>
          )}
          {global.data ? <ResponseChart series={global.data} range={range} subject="All components" height={220} /> : <div className="skeleton h-[220px]" aria-hidden />}
        </section>
      )}

      <section aria-label="Incident history" className="mt-8">
        <h2 className="mb-3 text-sm font-semibold uppercase tracking-wider text-muted">Past incidents, last 30 days</h2>
        {recent.length === 0 ? (
          <p className="text-base text-muted">No incidents in the last 30 days.</p>
        ) : (
          <ul className="flex flex-col gap-4">
            {recent.map((incident) => (
              <li key={incident.id} className="card p-5">
                <div className="flex flex-wrap items-center gap-2">
                  <SeverityBadge severity={incident.severity} />
                  <span className="mono text-xs text-muted">
                    {new Date(incident.startedAt).toLocaleDateString('en-GB', { day: 'numeric', month: 'short' })} · lasted{' '}
                    {formatDuration(((incident.resolvedAt ?? now) - incident.startedAt) / 1000)}
                  </span>
                </div>
                <h3 className="mt-2 text-md">{incident.title}</h3>
                <Timeline incident={incident} />
              </li>
            ))}
          </ul>
        )}
      </section>

      <section aria-label="Subscribe" className="card mt-8 p-5">
        <h2 className="mb-1 text-md">Get updates by email</h2>
        <p className="mb-4 text-base text-muted">We email when an incident opens, changes or resolves. One click to unsubscribe.</p>
        <SubscribeForm title={page.title} />
      </section>

      <footer className="mt-10 flex flex-wrap items-center justify-between gap-3 text-sm text-muted">
        <span>{page.footer}</span>
        <span className="inline-flex items-center gap-2">
          <LogoMark height={18} label="" /> Powered by Pulse
        </span>
      </footer>
    </div>
  )
}
