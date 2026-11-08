import { useState } from 'react'
import type { DayUptime } from '@/data/types'
import { dayState, formatDuration, type DayState } from '@/lib/stats'

interface UptimeBarsProps {
  days: DayUptime[]
  /** Name of the component, for the accessible summary. */
  subject: string
  height?: number
}

const COLOR: Record<DayState, string> = {
  up: 'var(--status-up)',
  degraded: 'var(--status-degraded)',
  down: 'var(--status-down)',
  none: 'var(--border-strong)',
}

function formatDay(iso: string): string {
  return new Date(`${iso}T00:00:00Z`).toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric', timeZone: 'UTC' })
}

/** One bar per day, colored by that day's uptime. Hover a bar for the date, uptime and downtime. */
export function UptimeBars({ days, subject, height = 40 }: UptimeBarsProps) {
  const [hover, setHover] = useState<number | null>(null)
  const hovered = hover === null ? null : days[hover]
  const badDays = days.filter((d) => dayState(d.uptimePct) === 'down' || dayState(d.uptimePct) === 'degraded')

  return (
    <div className="relative" onPointerLeave={() => setHover(null)}>
      <div
        role="img"
        aria-label={`${subject}: ${days.length} days of uptime. ${badDays.length} days with downtime.`}
        className="flex items-stretch gap-[2px]"
        style={{ height }}
      >
        {days.map((day, i) => (
          <div
            key={day.date}
            onPointerEnter={() => setHover(i)}
            className="flex-1 rounded-[3px] transition-transform duration-100 hover:scale-y-110"
            style={{ background: COLOR[dayState(day.uptimePct)], opacity: hover === null || hover === i ? 1 : 0.55 }}
          />
        ))}
      </div>

      {hovered && hover !== null && (
        <div
          role="presentation"
          className="pointer-events-none absolute bottom-full z-10 mb-2 w-44 -translate-x-1/2 rounded-xl bg-panel px-3 py-2 text-xs text-panel-fg shadow-overlay"
          style={{ left: `${((hover + 0.5) / days.length) * 100}%`, maxWidth: 'calc(100vw - 32px)' }}
        >
          <div className="font-medium">{formatDay(hovered.date)}</div>
          <div className="mono mt-0.5 text-panel-muted">
            {hovered.uptimePct === null ? 'No data' : `${hovered.uptimePct}% uptime`}
          </div>
          {hovered.downtimeS > 0 && (
            <div className="mono text-panel-muted">
              {formatDuration(hovered.downtimeS)} down · {hovered.incidents} {hovered.incidents === 1 ? 'incident' : 'incidents'}
            </div>
          )}
        </div>
      )}
    </div>
  )
}
