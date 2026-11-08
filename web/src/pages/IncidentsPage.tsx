import { Check, TriangleAlert } from 'lucide-react'
import { EmptyState } from '@/components/EmptyState'
import { PageHeader } from '@/components/PageHeader'
import { SeverityBadge } from '@/components/SeverityBadge'
import { useAcknowledgeIncident, useIncidents, useNow } from '@/data/queries'
import type { Incident, IncidentStatus } from '@/data/types'
import { formatDuration, relativeTime } from '@/lib/stats'

const STATUS_LABEL: Record<IncidentStatus, string> = {
  investigating: 'Investigating',
  identified: 'Identified',
  monitoring: 'Monitoring',
  resolved: 'Resolved',
}

function IncidentCard({ incident, now, index }: { incident: Incident; now: number; index: number }) {
  const acknowledge = useAcknowledgeIncident()
  const open = incident.status !== 'resolved'
  const duration = ((incident.resolvedAt ?? now) - incident.startedAt) / 1000

  return (
    <article className="card rise p-5" style={{ '--i': Math.min(index, 8) } as React.CSSProperties}>
      <header className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <SeverityBadge severity={incident.severity} />
            <span className="text-xs font-medium text-muted">{STATUS_LABEL[incident.status]}</span>
            {incident.isManual && <span className="text-xs text-subtle">Posted by hand</span>}
          </div>
          <h2 className="mt-2 text-md">{incident.title}</h2>
          <p className="mono mt-1 text-xs text-muted">
            {new Date(incident.startedAt).toLocaleString('en-GB')} · {open ? 'ongoing for' : 'lasted'}{' '}
            {formatDuration(duration)}
          </p>
        </div>
        {open &&
          (incident.acknowledgedAt ? (
            <span className="inline-flex items-center gap-1 text-xs font-medium text-muted">
              <Check size={14} strokeWidth={1.5} aria-hidden /> Acknowledged {relativeTime(incident.acknowledgedAt, now)}
            </span>
          ) : (
            <button type="button" className="btn" onClick={() => acknowledge.mutate(incident.id)}>
              Acknowledge
            </button>
          ))}
      </header>

      <ol className="mt-4 flex flex-col gap-3 border-l-2 border-border pl-4">
        {[...incident.updates].reverse().map((update) => (
          <li key={update.at} className="relative">
            <span aria-hidden className="absolute -left-[21px] top-1.5 size-2 rounded-full bg-border-strong" />
            <p className="text-sm">
              <span className="font-semibold">{STATUS_LABEL[update.status]}</span>
              <span className="mono ml-2 text-xs text-muted">{new Date(update.at).toLocaleString('en-GB')}</span>
            </p>
            <p className="mt-0.5 text-base text-muted">{update.message}</p>
          </li>
        ))}
      </ol>
    </article>
  )
}

export default function IncidentsPage() {
  const now = useNow()
  const { data, isPending } = useIncidents()
  const open = (data ?? []).filter((i) => i.status !== 'resolved')
  const resolved = (data ?? []).filter((i) => i.status === 'resolved')

  return (
    <>
      <PageHeader title="Incidents" description="Outages detected by monitors and incidents you post by hand." />

      {isPending ? (
        <div className="flex flex-col gap-4" aria-label="Loading incidents">
          <div className="skeleton h-40" aria-hidden />
          <div className="skeleton h-40" aria-hidden />
        </div>
      ) : (data ?? []).length === 0 ? (
        <EmptyState
          icon={TriangleAlert}
          title="No incidents"
          description="An incident opens on its own after consecutive failed checks, and resolves after consecutive successes."
        />
      ) : (
        <div className="flex flex-col gap-8">
          <section aria-labelledby="open-heading">
            <h2 id="open-heading" className="mb-3 text-md">Open ({open.length})</h2>
            {open.length === 0 ? (
              <p className="text-base text-muted">Nothing is open right now.</p>
            ) : (
              <div className="flex flex-col gap-4">
                {open.map((incident, i) => (
                  <IncidentCard key={incident.id} incident={incident} now={now} index={i} />
                ))}
              </div>
            )}
          </section>
          <section aria-labelledby="resolved-heading">
            <h2 id="resolved-heading" className="mb-3 text-md">Resolved ({resolved.length})</h2>
            <div className="flex flex-col gap-4">
              {resolved.map((incident, i) => (
                <IncidentCard key={incident.id} incident={incident} now={now} index={i} />
              ))}
            </div>
          </section>
        </div>
      )}
    </>
  )
}
