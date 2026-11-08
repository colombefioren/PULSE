/** A point on a response-time series. `ms` is null when the check failed. */
export interface SeriesPoint {
  t: number
  ms: number | null
}

export interface SeriesSummary {
  uptimePct: number
  avgMs: number
  p50: number
  p95: number
  p99: number
  checks: number
  failures: number
}

/** Uptime as a percentage with one decimal; 100 when there is nothing to divide by. */
export function uptimePercent(successes: number, total: number): number {
  if (total <= 0) return 100
  return Math.round((successes / total) * 1000) / 10
}

/** Nearest-rank percentile of already-collected values. Returns 0 for no values. */
export function percentile(values: number[], p: number): number {
  if (values.length === 0) return 0
  const sorted = [...values].sort((a, b) => a - b)
  const rank = Math.ceil((p / 100) * sorted.length)
  return sorted[Math.min(sorted.length - 1, Math.max(0, rank - 1))] ?? 0
}

export function summarize(series: SeriesPoint[]): SeriesSummary {
  const ok = series.flatMap((point) => (point.ms === null ? [] : [point.ms]))
  const avg = ok.length === 0 ? 0 : ok.reduce((sum, ms) => sum + ms, 0) / ok.length

  return {
    uptimePct: uptimePercent(ok.length, series.length),
    avgMs: Math.round(avg),
    p50: percentile(ok, 50),
    p95: percentile(ok, 95),
    p99: percentile(ok, 99),
    checks: series.length,
    failures: series.length - ok.length,
  }
}

export type DayState = 'up' | 'degraded' | 'down' | 'none'

/** Color bucket for one day on the 90-day bar. */
export function dayState(uptimePct: number | null): DayState {
  if (uptimePct === null) return 'none'
  if (uptimePct >= 99.9) return 'up'
  if (uptimePct >= 95) return 'degraded'
  return 'down'
}

const UNITS: [limitSeconds: number, divisor: number, suffix: string][] = [
  [60, 1, 's'],
  [3600, 60, 'm'],
  [86400, 3600, 'h'],
  [Infinity, 86400, 'd'],
]

/** "12s ago", "4m ago", "3h ago", "2d ago". Future or sub-second times read as "just now". */
export function relativeTime(thenMs: number, nowMs: number): string {
  const seconds = Math.floor((nowMs - thenMs) / 1000)
  if (seconds < 1) return 'just now'
  for (const [limit, divisor, suffix] of UNITS) {
    if (seconds < limit) return `${Math.floor(seconds / divisor)}${suffix} ago`
  }
  return 'just now'
}

/** "2h 14m", "45s", "3d 4h". Used for incident durations and downtime. */
export function formatDuration(totalSeconds: number): string {
  const s = Math.max(0, Math.round(totalSeconds))
  if (s < 60) return `${s}s`
  if (s < 3600) return `${Math.floor(s / 60)}m${s % 60 ? ` ${s % 60}s` : ''}`
  if (s < 86400) return `${Math.floor(s / 3600)}h${Math.floor((s % 3600) / 60) ? ` ${Math.floor((s % 3600) / 60)}m` : ''}`
  return `${Math.floor(s / 86400)}d${Math.floor((s % 86400) / 3600) ? ` ${Math.floor((s % 86400) / 3600)}h` : ''}`
}

export function formatMs(ms: number | null): string {
  return ms === null ? '-' : `${Math.round(ms)} ms`
}

/** 100%, 99.98%, 87.3%: two decimals once the value is in the nines, where they matter. */
export function formatUptime(value: number): string {
  if (value >= 100) return '100%'
  return `${value.toFixed(value >= 99 ? 2 : 1)}%`
}
