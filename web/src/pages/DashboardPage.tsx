import { ArrowRight, Check } from 'lucide-react'
import { useMemo, useState } from 'react'
import { Link } from 'react-router'
import { RingGauge } from '@/components/charts/RingGauge'
import { ResponseChart } from '@/components/charts/ResponseChart'
import { Sparkline } from '@/components/charts/Sparkline'
import { Panel } from '@/components/Panel'
import { RangePicker } from '@/components/RangePicker'
import { SeverityBadge } from '@/components/SeverityBadge'
import { useSession } from '@/session/useSession'
import { useAcknowledge, useIncidentAccess, useIncidentList, useOrgSeries, useSummaryView } from '@/data/incidents'
import { useMonitorList } from '@/data/monitors'
import { useNow } from '@/data/queries'
import type { ChartRange } from '@/data/types'
import { formatMs, relativeTime, summarize } from '@/lib/stats'

function greeting(hour: number): string {
  if (hour < 5) return 'Still up'
  if (hour < 12) return 'Good morning'
  if (hour < 18) return 'Good afternoon'
  return 'Good evening'
}

function Stat({ label, value, tone, index }: { label: string; value: number | string; tone?: string; index: number }) {
  return (
    <div className="card rise flex flex-col gap-1 px-5 py-4" style={{ '--i': index } as React.CSSProperties}>
      <span className="text-sm text-muted">{label}</span>
      <span className="mono text-xl font-semibold" style={tone ? { color: tone } : undefined}>
        {value}
      </span>
    </div>
  )
}

function Skeleton({ className }: { className: string }) {
  return <div className={`skeleton ${className}`} aria-hidden />
}

export default function DashboardPage() {
  const session = useSession()
  const user = session.status === 'ready' ? session.user : null
  const org = session.status === 'ready' ? session.org : null
  const now = useNow()
  const monitors = useMonitorList()
  const incidents = useIncidentList('open')
  const summary = useSummaryView()
  const acknowledge = useAcknowledge()
  const { canEdit, demoMode } = useIncidentAccess()
  const [range, setRange] = useState<ChartRange>('24h')
  const global = useOrgSeries(range)

  const open = incidents.data?.pages.flatMap((page) => page.items) ?? []
  const lastCheck = Math.max(0, ...(monitors.data ?? []).map((m) => m.lastCheckedAt ?? 0))
  const stats = useMemo(() => (global.data ? summarize(global.data) : null), [global.data])

  return (
    <div className="grid gap-4 xl:grid-cols-[minmax(0,1fr)_300px]">
      <div className="flex min-w-0 flex-col gap-4">
        <header className="rise flex flex-wrap items-end justify-between gap-3 pb-2">
          <div>
            <p className="text-sm text-muted">{org?.name}</p>
            <h1 className="text-2xl font-semibold">
              {greeting(new Date(now).getHours())}, {user?.name}
            </h1>
          </div>
          {lastCheck > 0 && (
            <p className="mono flex items-center gap-2 text-xs text-muted" aria-live="off">
              <span className="status-dot" data-status="up" aria-hidden />
              Live · last check {relativeTime(lastCheck, now)}
            </p>
          )}
        </header>

        <div className="grid grid-cols-2 gap-3 md:grid-cols-4">
          {summary.data ? (
            <>
              <Stat index={1} label="Monitors up" value={summary.data.up} tone="var(--status-up)" />
              <Stat index={2} label="Monitors down" value={summary.data.down} tone={summary.data.down ? 'var(--status-down)' : undefined} />
              <Stat index={3} label="Paused" value={summary.data.paused} />
              <Stat index={4} label="Open incidents" value={summary.data.openIncidents} />
            </>
          ) : (
            [0, 1, 2, 3].map((i) => <Skeleton key={i} className="h-[76px] rounded-card" />)
          )}
        </div>

        <div className="grid gap-4 lg:grid-cols-2">
          <Panel index={5} title="Open incidents" action={<Link to="/incidents" className="text-sm text-muted hover:text-fg">All incidents</Link>}>
            {incidents.isPending ? (
              <Skeleton className="h-24" />
            ) : open.length === 0 ? (
              <p className="text-base text-muted">Nothing is down. Incidents appear here the moment a monitor fails its checks.</p>
            ) : (
              <ul className="flex flex-col gap-3">
                {open.map((incident) => (
                  <li key={incident.id} className="rounded-2xl bg-bg p-4">
                    <div className="flex items-start justify-between gap-3">
                      <div className="min-w-0">
                        <SeverityBadge severity={incident.severity} />
                        <p className="mt-2 text-base font-medium">
                          <Link to={`/incidents/${incident.id}`} className="hover:underline">{incident.title}</Link>
                        </p>
                        <p className="mono mt-1 text-xs text-muted">Started {relativeTime(incident.startedAt, now)}</p>
                      </div>
                      {incident.acknowledgedAt ? (
                        <span className="inline-flex items-center gap-1 text-xs font-medium text-muted">
                          <Check size={14} strokeWidth={1.5} aria-hidden /> Acknowledged
                        </span>
                      ) : (
                        (canEdit || demoMode) && (
                          <button type="button" className="btn" onClick={() => acknowledge.mutate(incident.id)}>
                            Acknowledge
                          </button>
                        )
                      )}
                    </div>
                  </li>
                ))}
              </ul>
            )}
          </Panel>

          <Panel index={6} title="Monitors" action={<Link to="/monitors" className="text-sm text-muted hover:text-fg">View all</Link>}>
            {monitors.isPending ? (
              <Skeleton className="h-48" />
            ) : (monitors.data ?? []).length === 0 ? (
              <p className="text-base text-muted">
                No monitors yet. <Link to="/monitors" className="underline">Add your first one</Link> and it is checked within a minute.
              </p>
            ) : (
              <ul className="flex flex-col">
                {(monitors.data ?? []).slice(0, 6).map((monitor) => (
                  <li key={monitor.id} className="flex items-center gap-3 border-t border-border py-2.5 first:border-t-0 first:pt-0">
                    <span className="status-dot" data-status={monitor.status} data-live aria-hidden />
                    <span className="min-w-0 flex-1 truncate text-sm font-medium">
                      {monitor.name}
                      <span className="sr-only"> is {monitor.status}</span>
                    </span>
                    <Sparkline points={monitor.spark} width={72} height={24} label={`${monitor.name}, last 24 hours`} />
                    <span className="mono w-16 text-right text-xs text-muted">{formatMs(monitor.lastResponseMs)}</span>
                  </li>
                ))}
              </ul>
            )}
          </Panel>
        </div>

        <Panel
          index={7}
          title="Response time"
          action={<RangePicker value={range} onChange={setRange} label="Response time range" />}
        >
          {stats && (
            <dl className="mb-3 flex flex-wrap gap-x-8 gap-y-2">
              {[
                ['Average', `${stats.avgMs} ms`],
                ['p50', `${stats.p50} ms`],
                ['p95', `${stats.p95} ms`],
                ['p99', `${stats.p99} ms`],
              ].map(([term, value]) => (
                <div key={term}>
                  <dt className="text-xs text-muted">{term}</dt>
                  <dd className="mono text-md font-semibold">{value}</dd>
                </div>
              ))}
            </dl>
          )}
          {global.data ? (
            <ResponseChart series={global.data} range={range} subject="All monitors" />
          ) : (
            <Skeleton className="h-[260px]" />
          )}
        </Panel>
      </div>

      <aside aria-label="Uptime" className="card-plum rise flex flex-col gap-3 p-3" style={{ '--i': 3 } as React.CSSProperties}>
        {[
          { label: 'Uptime, 24 hours', value: summary.data?.uptime24h ?? null, color: 'var(--on-dark-up)' },
          { label: 'Uptime, 7 days', value: summary.data?.uptime7d ?? null, color: 'var(--brand)' },
          { label: 'Uptime, 30 days', value: summary.data?.uptime30d ?? null, color: 'var(--on-dark-degraded)' },
        ].map(({ label, value, color }) => (
          <div key={label} className="flex flex-col items-center gap-3 rounded-[20px] bg-white/10 px-4 py-5">
            <h2 className="text-sm font-medium">{label}</h2>
            {!summary.data ? (
              <Skeleton className="size-28 rounded-full" />
            ) : value === null ? (
              <p className="flex size-28 items-center justify-center text-center text-sm opacity-80">No checks yet</p>
            ) : (
              <RingGauge value={value} label={label} color={color} textColor="var(--plum-text)" />
            )}
          </div>
        ))}
        <Link to="/monitors" className="mx-auto mb-1 mt-1 inline-flex items-center gap-2 text-sm font-medium hover:underline">
          See monitors <ArrowRight size={16} strokeWidth={1.5} aria-hidden />
        </Link>
      </aside>
    </div>
  )
}
