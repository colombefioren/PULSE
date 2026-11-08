import type { Severity } from '@/data/types'

const LABEL: Record<Severity, string> = {
  degraded: 'Degraded',
  partial: 'Partial outage',
  major: 'Major outage',
  maintenance: 'Maintenance',
}

const DOT: Record<Severity, string> = {
  degraded: 'degraded',
  partial: 'degraded',
  major: 'down',
  maintenance: 'maintenance',
}

export function SeverityBadge({ severity }: { severity: Severity }) {
  return (
    <span className="inline-flex items-center gap-1.5 rounded-full bg-bg px-2.5 py-1 text-xs font-medium">
      <span className="status-dot" data-status={DOT[severity]} aria-hidden />
      {LABEL[severity]}
    </span>
  )
}
