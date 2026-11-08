import { ArrowLeft, Check } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { Link, useParams } from 'react-router'
import { ApiError } from '@/api/client'
import { EmptyState } from '@/components/EmptyState'
import { Notice } from '@/components/Notice'
import { Panel } from '@/components/Panel'
import { SeverityBadge } from '@/components/SeverityBadge'
import { useAcknowledge, useAddUpdate, useIncidentAccess, useIncidentDetail } from '@/data/incidents'
import { CHANNEL_LABEL, STATUS_LABEL, type DeliveryView, type UpdateKind, type UpdateView } from '@/data/incidentView'
import { useNow } from '@/data/queries'
import type { IncidentStatus } from '@/data/types'
import { formatDuration, relativeTime } from '@/lib/stats'

const UPDATE_LABEL: Record<UpdateKind, string> = {
  opened: 'Opened',
  update: 'Update',
  note: 'Note',
  acknowledged: 'Acknowledged',
  resolved: 'Resolved',
}

const POSTABLE: IncidentStatus[] = ['investigating', 'identified', 'monitoring', 'resolved']

function Timeline({ updates }: { updates: UpdateView[] }) {
  return (
    <ol className="flex flex-col gap-4 border-l-2 border-border pl-4" aria-label="Timeline">
      {[...updates].reverse().map((update) => (
        <li key={update.id} className="relative">
          <span aria-hidden className="absolute -left-[21px] top-1.5 size-2 rounded-full bg-border-strong" />
          <p className="text-sm">
            <span className="font-semibold">{UPDATE_LABEL[update.kind]}</span>
            {update.kind === 'update' && <span className="ml-2 text-muted">{STATUS_LABEL[update.status]}</span>}
            {update.kind === 'note' && <span className="ml-2 text-xs text-subtle">Internal</span>}
            <span className="mono ml-2 text-xs text-muted">{new Date(update.at).toLocaleString('en-GB')}</span>
          </p>
          <p className="mt-0.5 whitespace-pre-wrap text-base text-muted">{update.message}</p>
          {update.author && <p className="mt-0.5 text-xs text-subtle">{update.author}</p>}
        </li>
      ))}
    </ol>
  )
}

function Alerts({ alerts }: { alerts: DeliveryView[] }) {
  if (alerts.length === 0) {
    return <p className="text-base text-muted">No alerts were sent for this incident. Link alert channels to the monitor to be told when it fails.</p>
  }
  return (
    <ul className="flex flex-col">
      {alerts.map((alert) => (
        <li key={alert.id} className="flex flex-wrap items-center justify-between gap-2 border-t border-border py-2.5 first:border-t-0 first:pt-0">
          <span className="text-sm">
            <span className="font-medium">{alert.channelName ?? 'Removed channel'}</span>
            <span className="ml-2 text-xs text-muted">
              {alert.channelType && CHANNEL_LABEL[alert.channelType]} · {alert.kind === 'opened' ? 'Went down' : 'Recovered'}
            </span>
          </span>
          <span className="mono text-xs text-muted">
            {alert.status === 'sent' ? 'Sent' : alert.status === 'failed' ? 'Failed' : `Retrying (attempt ${alert.attempts})`}
          </span>
        </li>
      ))}
    </ul>
  )
}

function PostUpdate({ id, manual }: { id: string; manual: boolean }) {
  const add = useAddUpdate(id)
  const [message, setMessage] = useState('')
  const [status, setStatus] = useState<IncidentStatus | 'note'>(manual ? 'investigating' : 'note')
  const [error, setError] = useState<string | null>(null)

  const submit = (event: FormEvent) => {
    event.preventDefault()
    if (!message.trim()) {
      setError('Write a message')
      return
    }
    setError(null)
    add.mutate(
      { message: message.trim(), status: status === 'note' ? undefined : status },
      { onSuccess: () => setMessage('') },
    )
  }

  const failure = add.error instanceof ApiError ? add.error.message : null

  return (
    <form onSubmit={submit} noValidate className="flex flex-col gap-4">
      {failure && <Notice tone="error">{failure}</Notice>}
      {manual && (
        <div className="field">
          <label htmlFor="update-kind" className="field-label">Post as</label>
          <select id="update-kind" className="input" value={status} onChange={(e) => setStatus(e.target.value as IncidentStatus | 'note')}>
            {POSTABLE.map((s) => <option key={s} value={s}>{STATUS_LABEL[s]}</option>)}
            <option value="note">Internal note</option>
          </select>
        </div>
      )}
      <div className="field">
        <label htmlFor="update-message" className="field-label">{manual ? 'Message' : 'Add a note'}</label>
        <textarea
          id="update-message"
          className="input min-h-20 py-3"
          aria-invalid={error ? true : undefined}
          aria-describedby={error ? 'update-error' : undefined}
          value={message}
          onChange={(e) => setMessage(e.target.value)}
        />
        {error && <p id="update-error" className="field-error" role="alert">{error}</p>}
      </div>
      <div className="flex justify-end">
        <button type="submit" className="btn btn-primary" disabled={add.isPending}>
          {add.isPending ? 'Posting…' : manual && status !== 'note' ? 'Post update' : 'Add note'}
        </button>
      </div>
    </form>
  )
}

export default function IncidentDetailPage() {
  const { id = '' } = useParams()
  const now = useNow()
  const { canEdit, demoMode } = useIncidentAccess()
  const { data, isPending, isError, refetch } = useIncidentDetail(id)
  const acknowledge = useAcknowledge()

  if (isPending) return <div className="skeleton h-64" aria-label="Loading incident" />
  if (isError) {
    return (
      <Notice tone="error">
        Could not load this incident.{' '}
        <button type="button" className="underline" onClick={() => void refetch()}>Try again</button>
      </Notice>
    )
  }
  if (!data) {
    return (
      <EmptyState
        icon={ArrowLeft}
        title="Incident not found"
        description="It may have belonged to another team, or been removed with its monitor."
        action={<Link to="/incidents" className="btn">Back to incidents</Link>}
      />
    )
  }

  const { incident, updates, alerts } = data
  const open = incident.resolvedAt === null
  const seconds = ((incident.resolvedAt ?? now) - incident.startedAt) / 1000

  return (
    <div className="flex flex-col gap-6">
      <Link to="/incidents" className="inline-flex w-fit items-center gap-1.5 text-sm text-muted hover:text-fg">
        <ArrowLeft size={16} strokeWidth={1.5} aria-hidden /> All incidents
      </Link>

      <header className="flex flex-wrap items-start justify-between gap-4">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <SeverityBadge severity={incident.severity} />
            <span className="text-xs font-medium text-muted">{STATUS_LABEL[incident.status]}</span>
            {incident.isManual && <span className="text-xs text-subtle">Posted by hand</span>}
          </div>
          <h1 className="mt-2 text-xl">{incident.title}</h1>
          <p className="mono mt-1 text-xs text-muted">
            {incident.monitorId && incident.monitorName && (
              <>
                <Link to={`/monitors/${incident.monitorId}`} className="hover:underline">{incident.monitorName}</Link> ·{' '}
              </>
            )}
            Started {new Date(incident.startedAt).toLocaleString('en-GB')} · {open ? 'ongoing for' : 'lasted'} {formatDuration(seconds)}
          </p>
        </div>
        {open &&
          (incident.acknowledgedAt ? (
            <span className="inline-flex items-center gap-1 text-sm font-medium text-muted">
              <Check size={16} strokeWidth={1.5} aria-hidden />
              Acknowledged {relativeTime(incident.acknowledgedAt, now)}
              {incident.acknowledgedBy && <> by {incident.acknowledgedBy}</>}
            </span>
          ) : (
            (canEdit || demoMode) && (
              <button type="button" className="btn btn-primary" onClick={() => acknowledge.mutate(incident.id)} disabled={acknowledge.isPending}>
                Acknowledge
              </button>
            )
          ))}
      </header>

      {acknowledge.error instanceof ApiError && <Notice tone="error">{acknowledge.error.message}</Notice>}

      <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_340px]">
        <Panel title="Timeline">
          <Timeline updates={updates} />
        </Panel>
        <div className="flex flex-col gap-4">
          {open && canEdit && (
            <Panel title={incident.isManual ? 'Post an update' : 'Notes'} index={1}>
              <PostUpdate id={incident.id} manual={incident.isManual} />
            </Panel>
          )}
          {!demoMode && (
            <Panel title="Alerts sent" index={2}>
              <Alerts alerts={alerts} />
            </Panel>
          )}
        </div>
      </div>
    </div>
  )
}
