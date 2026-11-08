import type { SeriesPoint } from './stats'

export interface Box {
  width: number
  height: number
  left: number
  right: number
  top: number
  bottom: number
}

/** Round a maximum up to a tidy axis ceiling: 87 -> 100, 240 -> 250, 1320 -> 1500. */
export function niceMax(value: number): number {
  if (value <= 0) return 100
  const magnitude = 10 ** Math.floor(Math.log10(value))
  const steps = [1, 1.5, 2, 2.5, 3, 4, 5, 7.5, 10]
  const step = steps.find((s) => s * magnitude >= value) ?? 10
  return step * magnitude
}

export interface Scales {
  x: (t: number) => number
  y: (ms: number) => number
  max: number
}

export function makeScales(series: SeriesPoint[], box: Box, forcedMax?: number): Scales {
  const first = series[0]?.t ?? 0
  const last = series[series.length - 1]?.t ?? first + 1
  const span = Math.max(1, last - first)
  const peak = series.reduce((m, p) => (p.ms !== null && p.ms > m ? p.ms : m), 0)
  const max = forcedMax ?? niceMax(peak * 1.1)
  const innerW = box.width - box.left - box.right
  const innerH = box.height - box.top - box.bottom

  return {
    max,
    x: (t) => box.left + ((t - first) / span) * innerW,
    y: (ms) => box.top + innerH - (Math.min(ms, max) / max) * innerH,
  }
}

/** Runs of consecutive successful checks, so a failed check leaves a visible gap in the line. */
export function runs(series: SeriesPoint[]): SeriesPoint[][] {
  const out: SeriesPoint[][] = []
  let current: SeriesPoint[] = []
  for (const point of series) {
    if (point.ms === null) {
      if (current.length) out.push(current)
      current = []
    } else {
      current.push(point)
    }
  }
  if (current.length) out.push(current)
  return out
}

const fmt = (n: number) => Math.round(n * 10) / 10

export function linePath(run: SeriesPoint[], scales: Scales): string {
  return run
    .map((p, i) => `${i === 0 ? 'M' : 'L'}${fmt(scales.x(p.t))} ${fmt(scales.y(p.ms ?? 0))}`)
    .join('')
}

/** The same run closed down to the baseline, for the soft fill under the line. */
export function areaPath(run: SeriesPoint[], scales: Scales, baselineY: number): string {
  const first = run[0]
  const last = run[run.length - 1]
  if (!first || !last) return ''
  return `${linePath(run, scales)}L${fmt(scales.x(last.t))} ${fmt(baselineY)}L${fmt(scales.x(first.t))} ${fmt(baselineY)}Z`
}

export interface Band {
  from: number
  to: number
}

/** Time spans where checks failed, as x positions, merged across consecutive failures. */
export function downtimeBands(series: SeriesPoint[], scales: Scales): Band[] {
  const bands: Band[] = []
  let start: number | null = null
  series.forEach((point, i) => {
    const failed = point.ms === null
    if (failed && start === null) start = i
    const closing = start !== null && (!failed || i === series.length - 1)
    if (closing && start !== null) {
      const endIndex = failed ? i : i - 1
      const a = series[Math.max(0, start - 1)] ?? series[start]
      const b = series[Math.min(series.length - 1, endIndex + 1)] ?? series[endIndex]
      if (a && b) bands.push({ from: scales.x(a.t), to: scales.x(b.t) })
      start = null
    }
  })
  return bands
}

/** Index of the point whose x is closest to the pointer. */
export function nearestIndex(series: SeriesPoint[], scales: Scales, x: number): number {
  let best = 0
  let bestDistance = Infinity
  series.forEach((point, i) => {
    const distance = Math.abs(scales.x(point.t) - x)
    if (distance < bestDistance) {
      best = i
      bestDistance = distance
    }
  })
  return best
}
