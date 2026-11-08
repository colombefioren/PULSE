import type { MonitorStatus } from '@/data/types'

type PillStatus = MonitorStatus | 'degraded' | 'maintenance'

const LABEL: Record<PillStatus, string> = {
  up: 'Up',
  down: 'Down',
  paused: 'Paused',
  pending: 'Pending',
  degraded: 'Degraded',
  maintenance: 'Maintenance',
}

const DOT: Record<PillStatus, string> = {
  up: 'up',
  down: 'down',
  paused: 'paused',
  pending: 'paused',
  degraded: 'degraded',
  maintenance: 'maintenance',
}

/** Status as a dot plus a word, so color is never the only signal. */
export function StatusPill({ status, live = true }: { status: PillStatus; live?: boolean }) {
  return (
    <span className="inline-flex items-center gap-2 text-sm font-medium">
      <span className="status-dot" data-status={DOT[status]} data-live={live} aria-hidden />
      {LABEL[status]}
    </span>
  )
}
