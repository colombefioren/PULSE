import { Activity, ArrowLeft, Pause, Pencil, Play, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { ApiError } from '@/api/client'
import { ResponseChart } from '@/components/charts/ResponseChart'
import { ConfirmDialog } from '@/components/ConfirmDialog'
import { EmptyState } from '@/components/EmptyState'
import { MonitorFormDialog } from '@/components/MonitorFormDialog'
import { Panel } from '@/components/Panel'
import { RangePicker } from '@/components/RangePicker'
import { SeverityBadge } from '@/components/SeverityBadge'
import { StatusPill } from '@/components/StatusPill'
import { useDeleteMonitor, useMonitorAccess, useMonitorChecks, useMonitorStats, useMonitorView, useSetPaused } from '@/data/monitors'
import { useIncidents, useNow } from '@/data/queries'
import type { ChartRange } from '@/data/types'
import { formatDuration, formatMs, relativeTime } from '@/lib/stats'

type Tab = 'checks' | 'incidents'

const percent = (value: number | null) => (value === null ? '-' : `${value.toFixed(1)}%`)
const millis = (value: number | null) => (value === null ? '-' : `${value} ms`)

function Kpi({ label, value, index }: { label: string; value: string; index: number }) {
  return (
    <div className="card rise px-5 py-4" style={{ '--i': index } as React.CSSProperties}>
      <div className="text-sm text-muted">{label}</div>
      <div className="mono mt-1 text-xl font-semibold">{value}</div>
    </div>
  )
}

export default function MonitorDetailPage() {
  const { id = '' } = useParams()
  const navigate = useNavigate()
  const now = useNow()
  const { demoMode, canEdit, canPause } = useMonitorAccess()
  const monitor = useMonitorView(id)
  const [range, setRange] = useState<ChartRange>('24h')
  const stats = useMonitorStats(id, range)
  const checks = useMonitorChecks(id)
  const incidents = useIncidents()
  const setPaused = useSetPaused()
  const remove = useDeleteMonitor()
  const [tab, setTab] = useState<Tab>('checks')
  const [editing, setEditing] = useState(false)
  const [deleting, setDeleting] = useState(false)

  const series = stats.data?.series
  const recent = checks.data?.pages.flatMap((page) => page.items) ?? []
  const mine = (incidents.data ?? []).filter((incident) => incident.monitorId === id)
  const tabs: Tab[] = demoMode ? ['checks', 'incidents'] : ['checks']

  if (monitor.isError) {
    return (
      <EmptyState
        icon={Activity}
        title="Could not load this monitor"
        description="Check your connection and try again."
        action={
          <button type="button" className="btn" onClick={() => void monitor.refetch()}>
            Try again
          </button>
        }
      />
    )
  }

  if (monitor.isPending) {
    return (
      <div className="flex flex-col gap-4" aria-label="Loading monitor">
        <div className="skeleton h-16" aria-hidden />
        <div className="skeleton h-24" aria-hidden />
        <div className="skeleton h-72" aria-hidden />
      </div>
    )
  }

  const m = monitor.data
  if (!m) {
    return (
      <EmptyState
        icon={Activity}
        title="Monitor not found"
        description="It may have been deleted, or the link is wrong."
        action={<Link to="/monitors" className="btn">Back to monitors</Link>}
      />
    )
  }

  const paused = m.status === 'paused'

  return (
    <div className="flex flex-col gap-4">
      <div>
        <Link to="/monitors" className="mb-3 inline-flex items-center gap-2 text-sm text-muted hover:text-fg">
          <ArrowLeft size={16} strokeWidth={1.5} aria-hidden /> Monitors
        </Link>
        <header className="flex flex-wrap items-start justify-between gap-4">
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-3">
              <h1 className="text-2xl font-semibold">{m.name}</h1>
              <StatusPill status={m.status} />
            </div>
            <p className="mono mt-1 truncate text-sm text-muted">
              {m.method} {m.url} · every {m.intervalS < 60 ? `${m.intervalS}s` : `${m.intervalS / 60}m`}
              {!paused && m.lastCheckedAt !== null && ` · checked ${relativeTime(m.lastCheckedAt, now)}`}
            </p>
          </div>
          <div className="flex flex-wrap gap-2">
            {canPause && (
              <button type="button" className="btn" onClick={() => setPaused.mutate({ id: m.id, paused: !paused })}>
                {paused ? <Play size={16} strokeWidth={1.5} aria-hidden /> : <Pause size={16} strokeWidth={1.5} aria-hidden />}
                {paused ? 'Resume' : 'Pause'}
              </button>
            )}
            {canEdit && (
              <>
                <button type="button" className="btn" onClick={() => setEditing(true)}>
                  <Pencil size={16} strokeWidth={1.5} aria-hidden /> Edit
                </button>
                <button type="button" className="btn" onClick={() => setDeleting(true)}>
                  <Trash2 size={16} strokeWidth={1.5} aria-hidden /> Delete
                </button>
              </>
            )}
          </div>
        </header>
        {m.status === 'down' && m.lastError && (
          <p className="mt-3 rounded-2xl border border-down/30 bg-down/10 px-4 py-3 text-sm" role="status">
            {m.lastError}
          </p>
        )}
      </div>

      <div className="grid grid-cols-2 gap-3 md:grid-cols-3 xl:grid-cols-6">
        <Kpi index={1} label="Uptime, 24h" value={percent(stats.data?.uptime24h ?? null)} />
        <Kpi index={2} label="Uptime, 7d" value={percent(stats.data?.uptime7d ?? null)} />
        <Kpi index={3} label="Uptime, 30d" value={percent(stats.data?.uptime30d ?? null)} />
        <Kpi index={4} label={`Average, ${range}`} value={millis(stats.data?.avgMs ?? null)} />
        <Kpi index={5} label={`p95, ${range}`} value={millis(stats.data?.p95 ?? null)} />
        <Kpi index={6} label={`p99, ${range}`} value={millis(stats.data?.p99 ?? null)} />
      </div>

      <Panel index={7} title="Response time" action={<RangePicker value={range} onChange={setRange} label="Response time range" />}>
        {series && series.length > 1 ? (
          <ResponseChart series={series} range={range} subject={m.name} />
        ) : stats.isPending ? (
          <div className="skeleton h-[260px]" aria-hidden />
        ) : (
          <p className="text-base text-muted">
            No checks yet. The first result appears within a minute; a paused monitor does not run until it is resumed.
          </p>
        )}
      </Panel>

      <Panel index={8} title="Activity">
        <div role="tablist" aria-label="Activity" className="mb-4 inline-flex rounded-full bg-surface-2 p-1">
          {tabs.map((value) => (
            <button
              key={value}
              type="button"
              role="tab"
              id={`tab-${value}`}
              aria-selected={tab === value}
              aria-controls={`panel-${value}`}
              onClick={() => setTab(value)}
              className={`h-7 rounded-full px-4 text-xs font-medium ${tab === value ? 'bg-panel text-panel-fg' : 'text-muted hover:text-fg'}`}
            >
              {value === 'checks' ? 'Recent checks' : `Incidents (${mine.length})`}
            </button>
          ))}
        </div>

        {tab === 'checks' || !demoMode ? (
          <div role="tabpanel" id="panel-checks" aria-labelledby="tab-checks">
            {recent.length === 0 ? (
              <p className="text-base text-muted">{checks.isPending ? 'Loading checks…' : 'No checks to show.'}</p>
            ) : (
              <>
                <table className="w-full text-left text-sm">
                  <thead>
                    <tr className="text-xs text-muted">
                      <th scope="col" className="pb-2 font-medium">Time</th>
                      <th scope="col" className="pb-2 font-medium">Result</th>
                      <th scope="col" className="pb-2 text-right font-medium">Response</th>
                    </tr>
                  </thead>
                  <tbody>
                    {recent.map((check) => (
                      <tr key={check.at} className="border-t border-border">
                        <td className="mono py-2 align-top">{new Date(check.at).toLocaleTimeString('en-GB')}</td>
                        <td className="py-2">
                          {check.success ? (
                            <span className="text-up">{check.statusCode ? `${check.statusCode} OK` : 'OK'}</span>
                          ) : (
                            <span className="font-medium text-down">{check.message ?? 'The check failed'}</span>
                          )}
                        </td>
                        <td className="mono py-2 text-right align-top">{formatMs(check.responseMs)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
                {checks.hasNextPage && (
                  <button type="button" className="btn mt-4" disabled={checks.isFetchingNextPage} onClick={() => void checks.fetchNextPage()}>
                    {checks.isFetchingNextPage ? 'Loading…' : 'Load more'}
                  </button>
                )}
              </>
            )}
          </div>
        ) : (
          <div role="tabpanel" id="panel-incidents" aria-labelledby="tab-incidents">
            {mine.length === 0 ? (
              <p className="text-base text-muted">No incidents for this monitor.</p>
            ) : (
              <ul className="flex flex-col gap-3">
                {mine.map((incident) => (
                  <li key={incident.id} className="rounded-2xl bg-bg p-4">
                    <SeverityBadge severity={incident.severity} />
                    <p className="mt-2 font-medium">{incident.title}</p>
                    <p className="mono mt-1 text-xs text-muted">
                      {new Date(incident.startedAt).toLocaleString('en-GB')} ·{' '}
                      {incident.resolvedAt
                        ? `lasted ${formatDuration((incident.resolvedAt - incident.startedAt) / 1000)}`
                        : `ongoing for ${formatDuration((now - incident.startedAt) / 1000)}`}
                    </p>
                  </li>
                ))}
              </ul>
            )}
          </div>
        )}
      </Panel>

      {canEdit && <MonitorFormDialog open={editing} onOpenChange={setEditing} monitor={m} />}
      <ConfirmDialog
        open={deleting}
        onOpenChange={(open) => {
          setDeleting(open)
          if (!open) remove.reset()
        }}
        title={`Delete ${m.name}?`}
        description="Its check history is deleted with it and it stops being monitored. This can't be undone."
        confirmLabel="Delete monitor"
        pending={remove.isPending}
        error={remove.error instanceof ApiError ? remove.error.message : remove.isError ? 'Could not delete the monitor. Try again.' : null}
        onConfirm={() => remove.mutate(m.id, { onSuccess: () => void navigate('/monitors', { replace: true }) })}
      />
    </div>
  )
}
