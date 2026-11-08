import type { InfiniteData, QueryClient } from '@tanstack/react-query'
import type { ApiMonitorState } from '@/api/types'
import { monitorKeys } from '@/data/monitors'
import type { CheckView, MonitorView } from '@/data/monitorView'
import type { MonitorStatus } from '@/data/types'

export interface CheckRecordedEvent {
  monitorId: string
  checkedAt: string
  success: boolean
  statusCode: number | null
  responseMs: number
  errorType: string | null
  errorMessage: string | null
  state: ApiMonitorState
}

export interface MonitorChangedEvent {
  monitorId: string
  kind: string
}

type ChecksPage = { items: CheckView[]; next: string | null }

const STATUS: Record<ApiMonitorState, MonitorStatus> = { Pending: 'pending', Up: 'up', Down: 'down', Paused: 'paused' }
/** How many checks the live feed keeps on the first page before "load more" takes over. */
const KEEP_CHECKS = 100

/** Shows a fresh check result on the screens without asking the server again. */
export function applyCheckRecorded(client: QueryClient, orgId: string, event: CheckRecordedEvent) {
  const at = Date.parse(event.checkedAt)
  const patch = (monitor: MonitorView): MonitorView =>
    monitor.id !== event.monitorId || (monitor.lastCheckedAt ?? 0) > at
      ? monitor
      : {
          ...monitor,
          status: STATUS[event.state],
          lastCheckedAt: at,
          lastResponseMs: event.responseMs,
          lastError: event.success ? null : event.errorMessage,
        }

  client.setQueryData<MonitorView[]>(monitorKeys.list(orgId), (list) => list?.map(patch))
  client.setQueryData<MonitorView | null>(monitorKeys.one(event.monitorId), (monitor) => (monitor ? patch(monitor) : monitor))

  const check: CheckView = {
    at,
    success: event.success,
    statusCode: event.statusCode,
    responseMs: event.responseMs,
    message: event.errorMessage,
  }
  client.setQueryData<InfiniteData<ChecksPage, string | null>>(monitorKeys.checks(event.monitorId), (data) => {
    const first = data?.pages[0]
    if (!data || !first || first.items.some((existing) => existing.at === at)) return data
    return { ...data, pages: [{ ...first, items: [check, ...first.items].slice(0, KEEP_CHECKS) }, ...data.pages.slice(1)] }
  })

  // Charts and percentiles are computed on the server; refresh them only where someone is looking.
  void client.invalidateQueries({ queryKey: [...monitorKeys.one(event.monitorId), 'stats'], refetchType: 'active' })
}

/** Another person (or another tab) created, edited, paused or removed a monitor. */
export function applyMonitorChanged(client: QueryClient, orgId: string, event: MonitorChangedEvent) {
  void client.invalidateQueries({ queryKey: monitorKeys.list(orgId) })
  if (event.kind === 'deleted') {
    client.removeQueries({ queryKey: monitorKeys.one(event.monitorId) })
    return
  }
  void client.invalidateQueries({ queryKey: monitorKeys.one(event.monitorId), exact: true })
  void client.invalidateQueries({ queryKey: monitorKeys.checks(event.monitorId) })
}
