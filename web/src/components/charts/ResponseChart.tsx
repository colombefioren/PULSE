import { useMemo, useRef, useState, type PointerEvent } from 'react'
import { areaPath, downtimeBands, linePath, makeScales, nearestIndex, runs, type Box } from '@/lib/chart'
import { formatMs, percentile, summarize, type SeriesPoint } from '@/lib/stats'
import type { ChartRange } from '@/data/types'
import { useElementWidth } from './useElementWidth'

interface ResponseChartProps {
  series: SeriesPoint[]
  range: ChartRange
  height?: number
  /** Name of what is plotted, for the text summary. */
  subject: string
}

const TIME_FORMAT: Record<ChartRange, Intl.DateTimeFormatOptions> = {
  '1h': { hour: '2-digit', minute: '2-digit' },
  '24h': { hour: '2-digit', minute: '2-digit' },
  '7d': { month: 'short', day: 'numeric' },
  '30d': { month: 'short', day: 'numeric' },
}

function formatTime(t: number, range: ChartRange): string {
  return new Date(t).toLocaleString('en-GB', TIME_FORMAT[range])
}

/** Response-time line with a soft fill, a p95 guide, downtime bands, hover readout and a text summary. */
export function ResponseChart({ series, range, height = 260, subject }: ResponseChartProps) {
  const ref = useRef<HTMLDivElement>(null)
  const width = useElementWidth(ref, 720)
  const [hover, setHover] = useState<number | null>(null)

  const box: Box = { width, height, left: 48, right: 12, top: 14, bottom: 28 }
  const baseline = height - box.bottom

  const { scales, lines, areas, bands, p95, summary } = useMemo(() => {
    const scales = makeScales(series, { width, height, left: 48, right: 12, top: 14, bottom: 28 })
    const segments = runs(series)
    const okValues = series.flatMap((p) => (p.ms === null ? [] : [p.ms]))
    return {
      scales,
      lines: segments.map((run) => linePath(run, scales)),
      areas: segments.map((run) => areaPath(run, scales, height - 28)),
      bands: downtimeBands(series, scales),
      p95: percentile(okValues, 95),
      summary: summarize(series),
    }
  }, [series, width, height])

  const ticks = [0, scales.max / 2, scales.max]
  const hovered = hover === null ? null : series[hover]

  const onMove = (event: PointerEvent<SVGSVGElement>) => {
    const rect = event.currentTarget.getBoundingClientRect()
    setHover(nearestIndex(series, scales, event.clientX - rect.left))
  }

  const first = series[0]
  const middle = series[Math.floor(series.length / 2)]
  const last = series[series.length - 1]

  return (
    <div ref={ref} className="relative w-full">
      <svg
        width={width}
        height={height}
        viewBox={`0 0 ${width} ${height}`}
        role="img"
        aria-label={`${subject} response time over the last ${range}`}
        onPointerMove={onMove}
        onPointerLeave={() => setHover(null)}
        className="touch-pan-y"
      >
        {ticks.map((tick) => (
          <g key={tick}>
            <line x1={box.left} x2={width - box.right} y1={scales.y(tick)} y2={scales.y(tick)} stroke="var(--chart-grid)" />
            <text x={box.left - 8} y={scales.y(tick) + 4} textAnchor="end" className="mono fill-subtle" fontSize={11}>
              {Math.round(tick)}
            </text>
          </g>
        ))}

        {bands.map((band) => (
          <rect
            key={band.from}
            x={band.from}
            y={box.top}
            width={Math.max(2, band.to - band.from)}
            height={baseline - box.top}
            fill="var(--status-down)"
            opacity={0.14}
          />
        ))}

        {p95 > 0 && (
          <g>
            <line
              x1={box.left}
              x2={width - box.right}
              y1={scales.y(p95)}
              y2={scales.y(p95)}
              stroke="var(--text-subtle)"
              strokeDasharray="4 4"
            />
            <text x={width - box.right - 4} y={scales.y(p95) - 5} textAnchor="end" className="mono fill-subtle" fontSize={11}>
              p95 {Math.round(p95)} ms
            </text>
          </g>
        )}

        {areas.map((d) => (
          <path key={d} d={d} fill="var(--chart-area)" />
        ))}
        {lines.map((d) => (
          <path key={d} d={d} fill="none" stroke="var(--chart-line)" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round" />
        ))}

        {[first, middle, last].map((point, i) =>
          point ? (
            <text
              key={point.t}
              x={scales.x(point.t)}
              y={height - 8}
              textAnchor={i === 0 ? 'start' : i === 2 ? 'end' : 'middle'}
              className="mono fill-subtle"
              fontSize={11}
            >
              {formatTime(point.t, range)}
            </text>
          ) : null,
        )}

        {hovered && (
          <g pointerEvents="none">
            <line x1={scales.x(hovered.t)} x2={scales.x(hovered.t)} y1={box.top} y2={baseline} stroke="var(--text-subtle)" />
            {hovered.ms !== null && (
              <circle cx={scales.x(hovered.t)} cy={scales.y(hovered.ms)} r={5} fill="var(--brand)" stroke="var(--text)" strokeWidth={2} />
            )}
          </g>
        )}
      </svg>

      {hovered && (
        <div
          role="presentation"
          className="mono pointer-events-none absolute top-2 z-10 rounded-xl bg-panel px-3 py-2 text-xs text-panel-fg shadow-overlay"
          style={{ left: Math.min(Math.max(scales.x(hovered.t) - 60, 0), width - 130) }}
        >
          <div className="text-panel-muted">{formatTime(hovered.t, range)}</div>
          <div className={hovered.ms === null ? 'font-semibold text-[var(--on-dark-down)]' : 'font-semibold'}>
            {hovered.ms === null ? 'Check failed' : formatMs(hovered.ms)}
          </div>
        </div>
      )}

      <p className="sr-only">
        {subject}, last {range}: average {summary.avgMs} milliseconds, 95th percentile {summary.p95} milliseconds,{' '}
        {summary.failures} failed checks out of {summary.checks}.
      </p>
    </div>
  )
}
