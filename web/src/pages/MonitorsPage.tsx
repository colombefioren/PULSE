import { Activity, Pause, Play, Plus, Search } from 'lucide-react'
import { useEffect, useMemo, useRef, useState } from 'react'
import { Link, useSearchParams } from 'react-router'
import { Sparkline } from '@/components/charts/Sparkline'
import { EmptyState } from '@/components/EmptyState'
import { MonitorFormDialog } from '@/components/MonitorFormDialog'
import { Notice } from '@/components/Notice'
import { PageHeader } from '@/components/PageHeader'
import { StatusPill } from '@/components/StatusPill'
import { Tip } from '@/components/Tip'
import { useMonitorAccess, useMonitorList, useSetPaused } from '@/data/monitors'
import type { MonitorView } from '@/data/monitorView'
import { useNow } from '@/data/queries'
import { isTypingTarget } from '@/layout/shortcuts'
import { formatMs, relativeTime } from '@/lib/stats'

const INTERVAL_LABEL = (s: number) => (s < 60 ? `${s}s` : `${s / 60}m`)

function Row({ monitor, now, index, canPause }: { monitor: MonitorView; now: number; index: number; canPause: boolean }) {
  const setPaused = useSetPaused()
  const paused = monitor.status === 'paused'
  const hostname = monitor.url.replace(/^https?:\/\//, '')

  return (
    <tr className="rise group border-t border-border" style={{ '--i': Math.min(index, 8) } as React.CSSProperties}>
      <td className="py-3 pl-5 pr-3">
        <StatusPill status={monitor.status} />
      </td>
      <td className="max-w-[220px] py-3 pr-3">
        <Link to={`/monitors/${monitor.id}`} className="block truncate font-medium hover:underline">
          {monitor.name}
        </Link>
        <span className="mono block truncate text-xs text-subtle">{hostname}</span>
      </td>
      <td className="hidden py-3 pr-3 md:table-cell">
        <Sparkline points={monitor.spark} label={`${monitor.name}, last 24 hours`} />
      </td>
      <td className="mono hidden py-3 pr-3 text-right text-sm lg:table-cell">{monitor.uptime24h === null ? '-' : `${monitor.uptime24h.toFixed(1)}%`}</td>
      <td className="mono py-3 pr-3 text-right text-sm">{formatMs(monitor.lastResponseMs)}</td>
      <td className="mono hidden py-3 pr-3 text-right text-xs text-muted sm:table-cell">
        {paused || monitor.lastCheckedAt === null ? '-' : relativeTime(monitor.lastCheckedAt, now)}
        <span className="block text-subtle">every {INTERVAL_LABEL(monitor.intervalS)}</span>
      </td>
      <td className="py-3 pr-4 text-right">
        {canPause && (
          <Tip label={paused ? 'Resume monitor' : 'Pause monitor'}>
            <button
              type="button"
              className="btn btn-ghost btn-icon"
              aria-label={`${paused ? 'Resume' : 'Pause'} ${monitor.name}`}
              onClick={() => setPaused.mutate({ id: monitor.id, paused: !paused })}
            >
              {paused ? <Play size={16} strokeWidth={1.5} aria-hidden /> : <Pause size={16} strokeWidth={1.5} aria-hidden />}
            </button>
          </Tip>
        )}
      </td>
    </tr>
  )
}

export default function MonitorsPage() {
  const now = useNow()
  const { data, isPending, isError, refetch } = useMonitorList()
  const { canEdit, canPause } = useMonitorAccess()
  const [params, setParams] = useSearchParams()
  const [query, setQuery] = useState('')
  const adding = canEdit && params.get('new') === '1'
  const setAdding = (open: boolean) => setParams(open ? { new: '1' } : {}, { replace: true })
  const search = useRef<HTMLInputElement>(null)

  // "/" jumps to the search box from anywhere on the page.
  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if (event.key === '/' && !event.metaKey && !event.ctrlKey && !isTypingTarget(event.target)) {
        event.preventDefault()
        search.current?.focus()
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [])

  const visible = useMemo(() => {
    const needle = query.trim().toLowerCase()
    return (data ?? []).filter((m) => !needle || m.name.toLowerCase().includes(needle) || m.url.toLowerCase().includes(needle))
  }, [data, query])

  return (
    <>
      <PageHeader
        title="Monitors"
        description="Websites and APIs checked on a schedule."
        actions={
          canEdit && (
            <button type="button" className="btn btn-primary" onClick={() => setAdding(true)}>
              <Plus size={16} strokeWidth={1.5} aria-hidden /> Add monitor
            </button>
          )
        }
      />
      <MonitorFormDialog open={adding} onOpenChange={setAdding} />

      <div className="mb-4 flex items-center gap-3">
        <label className="relative block w-full max-w-sm">
          <span className="sr-only">Search monitors</span>
          <Search size={16} strokeWidth={1.5} aria-hidden className="pointer-events-none absolute left-4 top-1/2 -translate-y-1/2 text-subtle" />
          <input
            ref={search}
            className="input pl-11 pr-12"
            placeholder="Search by name or URL"
            value={query}
            onChange={(event) => setQuery(event.target.value)}
          />
          <kbd aria-hidden className="kbd pointer-events-none absolute right-3 top-1/2 -translate-y-1/2">/</kbd>
        </label>
      </div>

      {isError ? (
        <Notice tone="error">
          Could not load your monitors.{' '}
          <button type="button" className="underline" onClick={() => void refetch()}>Try again</button>
        </Notice>
      ) : isPending ? (
        <div className="card flex flex-col gap-3 p-5" aria-label="Loading monitors">
          {[0, 1, 2, 3, 4].map((i) => (
            <div key={i} className="skeleton h-12" aria-hidden />
          ))}
        </div>
      ) : (data ?? []).length === 0 ? (
        <EmptyState
          icon={Activity}
          title="No monitors yet"
          description={
            canEdit
              ? 'Add a URL and Pulse checks it every 30 seconds to 30 minutes, then alerts your team when it goes down.'
              : 'An Owner or Admin of this team can add the first one.'
          }
          action={
            canEdit ? (
              <button type="button" className="btn btn-primary" onClick={() => setAdding(true)}>
                <Plus size={16} strokeWidth={1.5} aria-hidden /> Add monitor
              </button>
            ) : undefined
          }
        />
      ) : visible.length === 0 ? (
        <EmptyState icon={Search} title={`No monitors match "${query}"`} description="Try part of the name or the hostname." />
      ) : (
        <div className="card overflow-hidden">
          <table className="w-full text-left text-base">
            <thead>
              <tr className="text-xs text-muted">
                <th scope="col" className="py-3 pl-5 pr-3 font-medium">Status</th>
                <th scope="col" className="py-3 pr-3 font-medium">Monitor</th>
                <th scope="col" className="hidden py-3 pr-3 font-medium md:table-cell">Last 24 hours</th>
                <th scope="col" className="hidden py-3 pr-3 text-right font-medium lg:table-cell">Uptime</th>
                <th scope="col" className="py-3 pr-3 text-right font-medium">Response</th>
                <th scope="col" className="hidden py-3 pr-3 text-right font-medium sm:table-cell">Last check</th>
                <th scope="col" className="py-3 pr-4"><span className="sr-only">Actions</span></th>
              </tr>
            </thead>
            <tbody>
              {visible.map((monitor, index) => (
                <Row key={monitor.id} monitor={monitor} now={now} index={index} canPause={canPause} />
              ))}
            </tbody>
          </table>
        </div>
      )}
    </>
  )
}
