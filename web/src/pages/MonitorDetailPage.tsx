import { ArrowLeft, Pause, Play } from 'lucide-react'
import { useMemo, useState } from 'react'
import { Link, useParams } from 'react-router'
import { ResponseChart } from '@/components/charts/ResponseChart'
import { EmptyState } from '@/components/EmptyState'
import { Panel } from '@/components/Panel'
import { RangePicker } from '@/components/RangePicker'
import { SeverityBadge } from '@/components/SeverityBadge'
import { StatusPill } from '@/components/StatusPill'
import { useIncidents, useMonitor, useNow, useSeries, useSetMonitorPaused } from '@/data/queries'
import type { ChartRange } from '@/data/types'
import { formatDuration, formatMs, relativeTime, summarize } from '@/lib/stats'
import { Activity } from 'lucide-react'

type Tab = 'checks' | 'incidents'

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
  const now = useNow()
  const monitor = useMonitor(id)
  const [range, setRange] = useState<ChartRange>('24h')
  const series = useSeries(id, range)
  const recent = useSeries(id, '1h')
  const incidents = useIncidents()
  const setPaused = useSetMonitorPaused()
  const [tab, setTab] = useState<Tab>('checks')

  const summary = useMemo(() => (series.data ? summarize(series.data) : null), [series.data])
  const mine = (incidents.data ?? []).filter((incident) => incident.monitorId === id)

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
              {!paused && ` · checked ${relativeTime(m.lastCheckedAt, now)}`}
            </p>
          </div>
          <button type="button" className="btn" onClick={() => setPaused.mutate({ id: m.id, paused: !paused })}>
            {paused ? <Play size={16} strokeWidth={1.5} aria-hidden /> : <Pause size={16} strokeWidth={1.5} aria-hidden />}
            {paused ? 'Resume' : 'Pause'}
          </button>
        </header>
      </div>

      <div className="grid grid-cols-2 gap-3 md:grid-cols-3 xl:grid-cols-6">
        <Kpi index={1} label="Uptime, 24h" value={`${m.uptime24h.toFixed(1)}%`} />
        <Kpi index={2} label="Uptime, 7d" value={`${m.uptime7d.toFixed(1)}%`} />
        <Kpi index={3} label="Uptime, 30d" value={`${m.uptime30d.toFixed(1)}%`} />
        <Kpi index={4} label="Uptime, 90d" value={`${m.uptime90d.toFixed(1)}%`} />
        <Kpi index={5} label={`Average, ${range}`} value={summary ? `${summary.avgMs} ms` : '-'} />
        <Kpi index={6} label={`p95, ${range}`} value={summary ? `${summary.p95} ms` : '-'} />
      </div>

      <Panel index={7} title="Response time" action={<RangePicker value={range} onChange={setRange} label="Response time range" />}>
        {series.data && series.data.length > 1 ? (
          <ResponseChart series={series.data} range={range} subject={m.name} />
        ) : series.isPending ? (
          <div className="skeleton h-[260px]" aria-hidden />
        ) : (
          <p className="text-base text-muted">No checks yet. A paused monitor does not run until it is resumed.</p>
        )}
      </Panel>

      <Panel index={8} title="Activity">
        <div role="tablist" aria-label="Activity" className="mb-4 inline-flex rounded-full bg-surface-2 p-1">
          {(['checks', 'incidents'] as Tab[]).map((value) => (
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

        {tab === 'checks' ? (
          <div role="tabpanel" id="panel-checks" aria-labelledby="tab-checks">
            {(recent.data ?? []).length === 0 ? (
              <p className="text-base text-muted">No checks to show.</p>
            ) : (
              <table className="w-full text-left text-sm">
                <thead>
                  <tr className="text-xs text-muted">
                    <th scope="col" className="pb-2 font-medium">Time</th>
                    <th scope="col" className="pb-2 font-medium">Result</th>
                    <th scope="col" className="pb-2 text-right font-medium">Response</th>
                  </tr>
                </thead>
                <tbody>
                  {[...(recent.data ?? [])].reverse().slice(0, 12).map((point) => (
                    <tr key={point.t} className="border-t border-border">
                      <td className="mono py-2">{new Date(point.t).toLocaleTimeString('en-GB')}</td>
                      <td className="py-2">
                        {point.ms === null ? (
                          <span className="font-medium text-down">Timeout after 10s. The server did not respond.</span>
                        ) : (
                          <span className="text-up">200 OK</span>
                        )}
                      </td>
                      <td className="mono py-2 text-right">{formatMs(point.ms)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
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
    </div>
  )
}
