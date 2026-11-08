import type { ApiCheck, ApiMethod, ApiMonitor, ApiMonitorListItem, ApiMonitorState, ApiMonitorStats, MonitorInput } from '@/api/types'
import type { SeriesPoint } from '@/lib/stats'
import type { ChartRange, Monitor, MonitorStatus } from './types'

/**
 * What the monitor screens render. It looks the same whether the numbers come from the demo workspace or from
 * the API; missing data is `null` rather than a made-up zero.
 */
export interface MonitorView {
  id: string
  name: string
  url: string
  method: 'GET' | 'HEAD' | 'POST'
  intervalS: number
  timeoutMs: number
  expectedCodes: string
  keyword: string | null
  headers: Record<string, string>
  failureThreshold: number
  recoveryThreshold: number
  /** Alert channels that hear about this monitor's incidents. */
  alertChannelIds: string[]
  /** Whether the public uptime badge is switched on. */
  badgeEnabled: boolean
  status: MonitorStatus
  lastResponseMs: number | null
  /** Epoch ms. */
  lastCheckedAt: number | null
  lastError: string | null
  uptime24h: number | null
  /** Last 24 hours, one point per hour. `ms` is null where there was no passing check. */
  spark: SeriesPoint[]
}

export interface StatsView {
  checks: number
  uptimePct: number | null
  avgMs: number | null
  p50: number | null
  p95: number | null
  p99: number | null
  uptime24h: number | null
  uptime7d: number | null
  uptime30d: number | null
  uptime90d: number | null
  series: SeriesPoint[]
}

export interface CheckView {
  at: number
  success: boolean
  statusCode: number | null
  responseMs: number
  message: string | null
}

export const RANGE_MS: Record<ChartRange, number> = {
  '1h': 3_600_000,
  '24h': 86_400_000,
  '7d': 604_800_000,
  '30d': 2_592_000_000,
}

const STATUS: Record<ApiMonitorState, MonitorStatus> = { Pending: 'pending', Up: 'up', Down: 'down', Paused: 'paused' }
const METHOD: Record<ApiMethod, MonitorView['method']> = { Get: 'GET', Head: 'HEAD', Post: 'POST' }
export const API_METHOD: Record<MonitorView['method'], ApiMethod> = { GET: 'Get', HEAD: 'Head', POST: 'Post' }

const HOUR = 3_600_000

export function monitorFromApi(monitor: ApiMonitor, uptime24h: number | null = null, sparkline: (number | null)[] = [], now = Date.now()): MonitorView {
  return {
    id: monitor.id,
    name: monitor.name,
    url: monitor.url,
    method: METHOD[monitor.method],
    intervalS: monitor.intervalSeconds,
    timeoutMs: monitor.timeoutMs,
    expectedCodes: monitor.expectedCodes,
    keyword: monitor.keyword,
    headers: monitor.headers,
    failureThreshold: monitor.failureThreshold,
    recoveryThreshold: monitor.recoveryThreshold,
    alertChannelIds: monitor.alertChannelIds ?? [],
    badgeEnabled: monitor.badgeEnabled ?? false,
    status: STATUS[monitor.state],
    lastResponseMs: monitor.lastResponseMs,
    lastCheckedAt: monitor.lastCheckedAt ? Date.parse(monitor.lastCheckedAt) : null,
    lastError: monitor.lastErrorMessage,
    uptime24h,
    spark: sparkline.map((ms, index) => ({ t: now - (sparkline.length - 1 - index) * HOUR, ms })),
  }
}

export const listItemFromApi = (item: ApiMonitorListItem, now = Date.now()) =>
  monitorFromApi(item.monitor, item.uptime24h, item.sparkline, now)

export function statsFromApi(stats: ApiMonitorStats): StatsView {
  return {
    checks: stats.checks,
    uptimePct: stats.uptimePercent,
    avgMs: stats.avgMs,
    p50: stats.p50Ms,
    p95: stats.p95Ms,
    p99: stats.p99Ms,
    uptime24h: stats.uptime.last24Hours,
    uptime7d: stats.uptime.last7Days,
    uptime30d: stats.uptime.last30Days,
    uptime90d: stats.uptime.last90Days ?? null,
    series: stats.series.map((point) => ({ t: Date.parse(point.at), ms: point.avgMs })),
  }
}

export const checkFromApi = (check: ApiCheck): CheckView => ({
  at: Date.parse(check.checkedAt),
  success: check.success,
  statusCode: check.statusCode,
  responseMs: check.responseMs,
  message: check.errorMessage,
})

/** Turns the demo workspace's monitor into the shared shape. */
export function monitorFromDemo(monitor: Monitor): MonitorView {
  return {
    id: monitor.id,
    name: monitor.name,
    url: monitor.url,
    method: monitor.method,
    intervalS: monitor.intervalS,
    timeoutMs: 10_000,
    expectedCodes: '200-299',
    keyword: null,
    headers: {},
    failureThreshold: 3,
    recoveryThreshold: 2,
    alertChannelIds: [],
    badgeEnabled: false,
    status: monitor.status,
    lastResponseMs: monitor.lastResponseMs,
    lastCheckedAt: monitor.status === 'paused' ? null : monitor.lastCheckedAt,
    lastError: null,
    uptime24h: monitor.uptime24h,
    spark: monitor.spark,
  }
}

/** The values a form starts with for a new monitor. */
export const NEW_MONITOR: MonitorInput = {
  name: '',
  url: '',
  method: 'Get',
  intervalSeconds: 60,
  timeoutMs: 10_000,
  expectedCodes: null,
  keyword: null,
  headers: {},
  failureThreshold: 3,
  recoveryThreshold: 2,
  badgeEnabled: false,
}

export function inputFromView(view: MonitorView): MonitorInput {
  return {
    name: view.name,
    url: view.url,
    method: API_METHOD[view.method],
    intervalSeconds: view.intervalS,
    timeoutMs: view.timeoutMs,
    expectedCodes: view.expectedCodes === '200-299' ? null : view.expectedCodes,
    keyword: view.keyword,
    headers: view.headers,
    failureThreshold: view.failureThreshold,
    recoveryThreshold: view.recoveryThreshold,
    badgeEnabled: view.badgeEnabled,
    alertChannelIds: view.alertChannelIds,
  }
}

export function headersToText(headers: Record<string, string>): string {
  return Object.entries(headers)
    .map(([name, value]) => `${name}: ${value}`)
    .join('\n')
}

/** One `Name: value` per line. Returns the first line that cannot be read instead of guessing. */
export function parseHeaders(text: string): { headers: Record<string, string>; error: string | null } {
  const headers: Record<string, string> = {}
  for (const line of text.split('\n')) {
    if (!line.trim()) continue
    const colon = line.indexOf(':')
    if (colon < 1) return { headers, error: `"${line.trim()}" needs a name and a value, like X-Api-Key: abc` }
    headers[line.slice(0, colon).trim()] = line.slice(colon + 1).trim()
  }
  return { headers, error: null }
}
