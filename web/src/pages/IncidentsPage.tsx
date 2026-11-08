import { Check, Plus, TriangleAlert } from 'lucide-react'
import { useState } from 'react'
import { Link, useNavigate } from 'react-router'
import { DeclareIncidentDialog } from '@/components/DeclareIncidentDialog'
import { EmptyState } from '@/components/EmptyState'
import { Notice } from '@/components/Notice'
import { PageHeader } from '@/components/PageHeader'
import { SeverityBadge } from '@/components/SeverityBadge'
import { useAcknowledge, useIncidentAccess, useIncidentList, type IncidentState } from '@/data/incidents'
import { STATUS_LABEL, type IncidentView } from '@/data/incidentView'
import { useNow } from '@/data/queries'
import { formatDuration, relativeTime } from '@/lib/stats'

function Row({ incident, now, index, canAck }: { incident: IncidentView; now: number; index: number; canAck: boolean }) {
  const acknowledge = useAcknowledge()
  const open = incident.resolvedAt === null
  const seconds = ((incident.resolvedAt ?? now) - incident.startedAt) / 1000

  return (
    <li className="card rise flex flex-wrap items-start justify-between gap-3 p-5" style={{ '--i': Math.min(index, 8) } as React.CSSProperties}>
      <div className="min-w-0">
        <div className="flex flex-wrap items-center gap-2">
          <SeverityBadge severity={incident.severity} />
          <span className="text-xs font-medium text-muted">{STATUS_LABEL[incident.status]}</span>
          {incident.isManual && <span className="text-xs text-subtle">Posted by hand</span>}
        </div>
        <h3 className="mt-2 text-md">
          <Link to={`/incidents/${incident.id}`} className="hover:underline">{incident.title}</Link>
        </h3>
        <p className="mono mt-1 text-xs text-muted">
          {incident.monitorName && <>{incident.monitorName} · </>}
          {new Date(incident.startedAt).toLocaleString('en-GB')} · {open ? 'ongoing for' : 'lasted'} {formatDuration(seconds)}
        </p>
      </div>
      {open &&
        (incident.acknowledgedAt ? (
          <span className="inline-flex items-center gap-1 text-xs font-medium text-muted">
            <Check size={14} strokeWidth={1.5} aria-hidden />
            Acknowledged {relativeTime(incident.acknowledgedAt, now)}
            {incident.acknowledgedBy && <> by {incident.acknowledgedBy}</>}
          </span>
        ) : (
          canAck && (
            <button type="button" className="btn" onClick={() => acknowledge.mutate(incident.id)} disabled={acknowledge.isPending}>
              Acknowledge
            </button>
          )
        ))}
    </li>
  )
}

function Section({ state, title, empty, now, canAck }: { state: IncidentState; title: string; empty: string; now: number; canAck: boolean }) {
  const list = useIncidentList(state)
  const items = list.data?.pages.flatMap((page) => page.items) ?? []
  const heading = `${state}-heading`

  return (
    <section aria-labelledby={heading}>
      <h2 id={heading} className="mb-3 text-md">
        {list.data && !list.hasNextPage ? `${title} (${items.length})` : title}
      </h2>
      {list.isPending ? (
        <div className="skeleton h-28" aria-hidden />
      ) : list.isError ? (
        <Notice tone="error">
          Could not load incidents.{' '}
          <button type="button" className="underline" onClick={() => void list.refetch()}>Try again</button>
        </Notice>
      ) : items.length === 0 ? (
        <p className="text-base text-muted">{empty}</p>
      ) : (
        <>
          <ul className="flex flex-col gap-4">
            {items.map((incident, i) => (
              <Row key={incident.id} incident={incident} now={now} index={i} canAck={canAck} />
            ))}
          </ul>
          {list.hasNextPage && (
            <button type="button" className="btn mt-4" onClick={() => void list.fetchNextPage()} disabled={list.isFetchingNextPage}>
              {list.isFetchingNextPage ? 'Loading…' : 'Load more'}
            </button>
          )}
        </>
      )}
    </section>
  )
}

export default function IncidentsPage() {
  const now = useNow()
  const navigate = useNavigate()
  const { canEdit, demoMode } = useIncidentAccess()
  const [declaring, setDeclaring] = useState(false)
  const open = useIncidentList('open')
  const resolved = useIncidentList('resolved')
  const nothing =
    open.data && resolved.data && open.data.pages[0]?.items.length === 0 && resolved.data.pages[0]?.items.length === 0

  return (
    <>
      <PageHeader
        title="Incidents"
        description="Outages detected by monitors and incidents you post by hand."
        actions={
          canEdit && (
            <button type="button" className="btn btn-primary" onClick={() => setDeclaring(true)}>
              <Plus size={16} strokeWidth={1.5} aria-hidden /> Declare incident
            </button>
          )
        }
      />

      {nothing ? (
        <EmptyState
          icon={TriangleAlert}
          title="No incidents"
          description="An incident opens on its own after consecutive failed checks, and resolves after consecutive successes. Declare one by hand for anything the monitors cannot see."
          action={
            canEdit ? (
              <button type="button" className="btn" onClick={() => setDeclaring(true)}>Declare incident</button>
            ) : undefined
          }
        />
      ) : (
        <div className="flex flex-col gap-8">
          <Section state="open" title="Open" empty="Nothing is open right now." now={now} canAck={canEdit || demoMode} />
          <Section state="resolved" title="Resolved" empty="Resolved incidents appear here." now={now} canAck={canEdit || demoMode} />
        </div>
      )}

      <DeclareIncidentDialog open={declaring} onOpenChange={setDeclaring} onDeclared={(id) => void navigate(`/incidents/${id}`)} />
    </>
  )
}
