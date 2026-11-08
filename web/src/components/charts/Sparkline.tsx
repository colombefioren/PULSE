import { useMemo } from 'react'
import { linePath, makeScales, runs } from '@/lib/chart'
import type { SeriesPoint } from '@/lib/stats'

interface SparklineProps {
  points: SeriesPoint[]
  width?: number
  height?: number
  /** CSS color for the line. */
  color?: string
  label: string
}

/** Tiny response-time line for table rows. Failed checks leave gaps instead of dropping to zero. */
export function Sparkline({ points, width = 112, height = 32, color = 'var(--chart-line)', label }: SparklineProps) {
  const paths = useMemo(() => {
    const scales = makeScales(points, { width, height, left: 2, right: 2, top: 3, bottom: 3 })
    return runs(points).map((run) => linePath(run, scales))
  }, [points, width, height])

  if (points.length < 2) return <span className="text-xs text-subtle">No data</span>

  return (
    <svg width={width} height={height} viewBox={`0 0 ${width} ${height}`} role="img" aria-label={label}>
      {paths.map((d) => (
        <path key={d} d={d} fill="none" stroke={color} strokeWidth={1.5} strokeLinecap="round" strokeLinejoin="round" />
      ))}
    </svg>
  )
}
