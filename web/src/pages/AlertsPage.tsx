import { Bell, Copy, KeyRound, Pencil, Plus, Send, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { ApiError } from '@/api/client'
import { ChannelFormDialog } from '@/components/ChannelFormDialog'
import { ConfirmDialog } from '@/components/ConfirmDialog'
import { EmptyState } from '@/components/EmptyState'
import { Notice } from '@/components/Notice'
import { PageHeader } from '@/components/PageHeader'
import {
  useChannels,
  useDeleteChannel,
  useDeliveries,
  useRotateSecret,
  useTestChannel,
  type ChannelView,
  type SavedChannel,
} from '@/data/alerts'
import { useIncidentAccess } from '@/data/incidents'
import { CHANNEL_LABEL, type DeliveryView } from '@/data/incidentView'
import { useNow } from '@/data/queries'
import { relativeTime } from '@/lib/stats'

const KIND_LABEL: Record<DeliveryView['kind'], string> = { opened: 'Went down', resolved: 'Recovered', test: 'Test' }

function describe(channel: ChannelView): string {
  if (channel.type === 'email') return channel.recipients.join(', ')
  return channel.destination ?? ''
}

function DeliveryLine({ delivery, now }: { delivery: DeliveryView; now: number }) {
  const state =
    delivery.status === 'sent'
      ? 'Sent'
      : delivery.status === 'failed'
        ? 'Failed'
        : `Retrying, attempt ${delivery.attempts}`
  return (
    <li className="flex flex-wrap items-baseline justify-between gap-2 border-t border-border py-2 first:border-t-0">
      <span className="text-sm">
        {KIND_LABEL[delivery.kind]}
        <span className="mono ml-2 text-xs text-muted">{relativeTime(delivery.createdAt, now)}</span>
      </span>
      <span className="text-xs">
        <span className="font-medium" data-status={delivery.status}>{state}</span>
        {delivery.error && delivery.status !== 'sent' && <span className="ml-2 text-muted">{delivery.error}</span>}
      </span>
    </li>
  )
}

function History({ channelId, now }: { channelId: string; now: number }) {
  const deliveries = useDeliveries(channelId)
  const items = deliveries.data?.pages.flatMap((page) => page.items) ?? []

  if (deliveries.isPending) return <div className="skeleton mt-4 h-16" aria-hidden />
  if (items.length === 0) return <p className="mt-4 text-sm text-muted">Nothing has been sent through this channel yet.</p>

  return (
    <div className="mt-4">
      <ul aria-label="Recent alerts">
        {items.map((delivery) => <DeliveryLine key={delivery.id} delivery={delivery} now={now} />)}
      </ul>
      {deliveries.hasNextPage && (
        <button type="button" className="btn mt-2" onClick={() => void deliveries.fetchNextPage()} disabled={deliveries.isFetchingNextPage}>
          Load more
        </button>
      )}
    </div>
  )
}

function SecretNotice({ secret, onClose }: { secret: string; onClose: () => void }) {
  const [copied, setCopied] = useState(false)
  const copy = async () => {
    try {
      await navigator.clipboard.writeText(secret)
      setCopied(true)
    } catch {
      // The secret is on screen to copy by hand.
    }
  }

  return (
    <Notice tone="success" className="flex flex-col gap-3">
      <p className="font-medium">Your signing secret. Copy it now: it is shown only once.</p>
      <p className="text-muted">
        Pulse signs every request with it. Check the <code className="mono">X-Pulse-Signature</code> header, which is{' '}
        <code className="mono">sha256=</code> and the HMAC-SHA256 of the raw body.
      </p>
      <div className="flex flex-wrap items-center gap-2">
        <code className="mono break-all rounded-control bg-bg px-3 py-2 text-sm" data-testid="signing-secret">{secret}</code>
        <button type="button" className="btn" onClick={() => void copy()}>
          <Copy size={16} strokeWidth={1.5} aria-hidden /> {copied ? 'Copied' : 'Copy'}
        </button>
        <button type="button" className="btn btn-ghost" onClick={onClose}>I have saved it</button>
      </div>
    </Notice>
  )
}

function ChannelCard({
  channel,
  canEdit,
  now,
  onEdit,
  onSecret,
  index,
}: {
  channel: ChannelView
  canEdit: boolean
  now: number
  onEdit: () => void
  onSecret: (secret: string) => void
  index: number
}) {
  const test = useTestChannel()
  const rotate = useRotateSecret()
  const remove = useDeleteChannel()
  const [history, setHistory] = useState(false)
  const [deleting, setDeleting] = useState(false)
  const result = test.data
  const failure = test.error instanceof ApiError ? test.error.message : rotate.error instanceof ApiError ? rotate.error.message : null

  return (
    <li className="card rise p-5" style={{ '--i': Math.min(index, 8) } as React.CSSProperties}>
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="text-xs font-medium text-muted">{CHANNEL_LABEL[channel.type]}</p>
          <h3 className="mt-0.5 text-md">{channel.name}</h3>
          <p className="mono mt-1 break-all text-xs text-muted">{describe(channel)}</p>
          <p className="mt-2 text-sm text-muted">
            {channel.monitorCount === 0 ? 'Not used by any monitor yet' : `Used by ${channel.monitorCount} ${channel.monitorCount === 1 ? 'monitor' : 'monitors'}`}
          </p>
        </div>
        {canEdit && (
          <div className="flex flex-wrap gap-2">
            <button type="button" className="btn" onClick={() => test.mutate(channel.id)} disabled={test.isPending}>
              <Send size={16} strokeWidth={1.5} aria-hidden /> {test.isPending ? 'Sending…' : 'Send test alert'}
            </button>
            {channel.type === 'webhook' && (
              <button
                type="button"
                className="btn"
                onClick={() => rotate.mutate(channel.id, { onSuccess: (saved) => saved.signingSecret && onSecret(saved.signingSecret) })}
                disabled={rotate.isPending}
              >
                <KeyRound size={16} strokeWidth={1.5} aria-hidden /> New secret
              </button>
            )}
            <button type="button" className="btn btn-ghost btn-icon" aria-label={`Edit ${channel.name}`} onClick={onEdit}>
              <Pencil size={16} strokeWidth={1.5} aria-hidden />
            </button>
            <button type="button" className="btn btn-ghost btn-icon" aria-label={`Delete ${channel.name}`} onClick={() => setDeleting(true)}>
              <Trash2 size={16} strokeWidth={1.5} aria-hidden />
            </button>
          </div>
        )}
      </div>

      {failure && <Notice tone="error" className="mt-4">{failure}</Notice>}
      {result && (
        <Notice tone={result.status === 'sent' ? 'success' : 'error'} className="mt-4">
          {result.status === 'sent' ? 'Test alert sent.' : `The test alert did not go through: ${result.error ?? 'the destination refused it'}.`}
        </Notice>
      )}

      {channel.lastDelivery && !result && (
        <p className="mt-3 text-xs text-muted">
          Last alert: {KIND_LABEL[channel.lastDelivery.kind].toLowerCase()}, {relativeTime(channel.lastDelivery.createdAt, now)}.{' '}
          {channel.lastDelivery.status === 'sent' ? 'Delivered.' : channel.lastDelivery.status === 'failed' ? 'Could not be delivered.' : 'Still retrying.'}
        </p>
      )}

      {!channel.id.startsWith('demo-') && (
        <>
          <button type="button" className="mt-3 text-sm text-muted underline hover:text-fg" aria-expanded={history} onClick={() => setHistory((v) => !v)}>
            {history ? 'Hide history' : 'Delivery history'}
          </button>
          {history && <History channelId={channel.id} now={now} />}
        </>
      )}

      <ConfirmDialog
        open={deleting}
        onOpenChange={setDeleting}
        title={`Delete ${channel.name}?`}
        description="Monitors stop sending alerts here. Alerts already sent stay in the incident history."
        confirmLabel="Delete channel"
        pending={remove.isPending}
        error={remove.error instanceof ApiError ? remove.error.message : null}
        onConfirm={() => remove.mutate(channel.id, { onSuccess: () => setDeleting(false) })}
      />
    </li>
  )
}

export default function AlertsPage() {
  const now = useNow()
  const { canEdit, demoMode } = useIncidentAccess()
  const channels = useChannels()
  const [adding, setAdding] = useState(false)
  const [editing, setEditing] = useState<ChannelView | null>(null)
  const [secret, setSecret] = useState<string | null>(null)

  const saved = (result: SavedChannel) => {
    if (result.signingSecret) setSecret(result.signingSecret)
  }

  return (
    <>
      <PageHeader
        title="Alerts"
        description="Where Pulse sends a message when a monitor goes down or recovers."
        actions={
          canEdit && (
            <button type="button" className="btn btn-primary" onClick={() => setAdding(true)}>
              <Plus size={16} strokeWidth={1.5} aria-hidden /> Add channel
            </button>
          )
        }
      />

      {demoMode && <Notice tone="success" className="mb-4">The demo shows two example channels. Sign up to connect your own.</Notice>}
      {secret && <div className="mb-4"><SecretNotice secret={secret} onClose={() => setSecret(null)} /></div>}

      {channels.isPending ? (
        <div className="skeleton h-32" aria-label="Loading channels" />
      ) : channels.isError ? (
        <Notice tone="error">
          Could not load alert channels.{' '}
          <button type="button" className="underline" onClick={() => void channels.refetch()}>Try again</button>
        </Notice>
      ) : channels.data.length === 0 ? (
        <EmptyState
          icon={Bell}
          title="No alert channels yet"
          description="Connect email, Discord, Slack or a signed webhook, then choose which monitors use each channel."
          action={canEdit ? <button type="button" className="btn btn-primary" onClick={() => setAdding(true)}>Add channel</button> : undefined}
        />
      ) : (
        <ul className="flex flex-col gap-4">
          {channels.data.map((channel, i) => (
            <ChannelCard key={channel.id} channel={channel} canEdit={canEdit} now={now} index={i} onEdit={() => setEditing(channel)} onSecret={setSecret} />
          ))}
        </ul>
      )}

      <ChannelFormDialog open={adding} onOpenChange={setAdding} onSaved={saved} />
      <ChannelFormDialog open={editing !== null} onOpenChange={(open) => !open && setEditing(null)} channel={editing ?? undefined} onSaved={saved} />
    </>
  )
}
