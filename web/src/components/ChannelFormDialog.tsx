import * as Dialog from '@radix-ui/react-dialog'
import { X } from 'lucide-react'
import { useId, useState, type FormEvent, type ReactNode } from 'react'
import { ApiError } from '@/api/client'
import { useSaveChannel, type ChannelView, type SavedChannel } from '@/data/alerts'
import { CHANNEL_LABEL, type ChannelType } from '@/data/incidentView'
import { Notice } from './Notice'
import { TextField } from './TextField'

const TYPES: ChannelType[] = ['email', 'slack', 'discord', 'webhook']

const URL_HINT: Record<Exclude<ChannelType, 'email'>, string> = {
  slack: 'The incoming webhook address from Slack, like https://hooks.slack.com/services/...',
  discord: 'The webhook address from your Discord channel settings, like https://discord.com/api/webhooks/...',
  webhook: 'Pulse sends a signed JSON POST here. Public https addresses only.',
}

function Field({ label, error, hint, children }: { label: string; error?: string; hint?: ReactNode; children: (id: string, describedBy?: string) => ReactNode }) {
  const id = useId()
  const describedBy = error ? `${id}-error` : hint ? `${id}-hint` : undefined
  return (
    <div className="field">
      <label htmlFor={id} className="field-label">{label}</label>
      {children(id, describedBy)}
      {error ? <p id={`${id}-error`} className="field-error" role="alert">{error}</p> : hint ? <p id={`${id}-hint`} className="field-hint">{hint}</p> : null}
    </div>
  )
}

function ChannelForm({ channel, onDone }: { channel?: ChannelView; onDone: (saved?: SavedChannel) => void }) {
  const save = useSaveChannel(channel?.id)
  const [type, setType] = useState<ChannelType>(channel?.type ?? 'email')
  const [name, setName] = useState(channel?.name ?? '')
  const [recipients, setRecipients] = useState(channel?.recipients.join('\n') ?? '')
  const [url, setUrl] = useState('')
  const [errors, setErrors] = useState<{ name?: string; recipients?: string; url?: string }>({})

  const failure = save.error instanceof ApiError ? save.error : null
  const server = (field: string) => failure?.fields[field]?.[0] ?? failure?.fields[field.charAt(0).toUpperCase() + field.slice(1)]?.[0]
  const banner = failure && Object.keys(failure.fields).length === 0 ? failure.message : null

  const submit = (event: FormEvent) => {
    event.preventDefault()
    const list = recipients.split(/[\s,;]+/).filter(Boolean)
    const found: typeof errors = {}
    if (!name.trim()) found.name = 'Enter a name'
    if (type === 'email' && list.length === 0) found.recipients = 'Enter at least one email address'
    if (type !== 'email' && !channel && !url.trim()) found.url = 'Enter the webhook address'
    setErrors(found)
    if (Object.keys(found).length > 0) return

    save.mutate({ type, name, recipients: list, url }, { onSuccess: (saved) => onDone(saved) })
  }

  return (
    <form onSubmit={submit} noValidate className="mt-4 flex flex-col gap-5">
      {banner && <Notice tone="error">{banner}</Notice>}

      <Field label="Type">
        {(id) => (
          <select id={id} className="input" value={type} disabled={Boolean(channel)} onChange={(e) => setType(e.target.value as ChannelType)}>
            {TYPES.map((t) => <option key={t} value={t}>{CHANNEL_LABEL[t]}</option>)}
          </select>
        )}
      </Field>

      <TextField label="Name" placeholder="On-call team" value={name} error={errors.name ?? server('name')} onChange={(e) => setName(e.target.value)} />

      {type === 'email' ? (
        <Field label="Send to" error={errors.recipients ?? server('recipients')} hint="One address per line, up to 10.">
          {(id, describedBy) => (
            <textarea
              id={id}
              className="input min-h-24 py-3"
              autoCapitalize="none"
              spellCheck={false}
              aria-invalid={errors.recipients || server('recipients') ? true : undefined}
              aria-describedby={describedBy}
              value={recipients}
              onChange={(e) => setRecipients(e.target.value)}
            />
          )}
        </Field>
      ) : (
        <TextField
          label="Webhook address"
          type="url"
          autoComplete="off"
          spellCheck={false}
          placeholder={channel ? `Leave blank to keep ${channel.destination ?? 'the current address'}` : 'https://'}
          hint={URL_HINT[type]}
          value={url}
          error={errors.url ?? server('url')}
          onChange={(e) => setUrl(e.target.value)}
        />
      )}

      <div className="flex justify-end gap-3">
        <button type="button" className="btn" onClick={() => onDone()}>Cancel</button>
        <button type="submit" className="btn btn-primary" disabled={save.isPending}>
          {save.isPending ? 'Saving…' : channel ? 'Save changes' : 'Add channel'}
        </button>
      </div>
    </form>
  )
}

interface ChannelFormDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  /** The channel being edited; leave out to add one. */
  channel?: ChannelView
  onSaved?: (saved: SavedChannel) => void
}

export function ChannelFormDialog({ open, onOpenChange, channel, onSaved }: ChannelFormDialogProps) {
  return (
    <Dialog.Root open={open} onOpenChange={onOpenChange}>
      <Dialog.Portal>
        <Dialog.Overlay className="fixed inset-0 z-50 bg-black/50" />
        <Dialog.Content
          aria-describedby={undefined}
          className="fixed left-1/2 top-1/2 z-50 max-h-[calc(100dvh-2rem)] w-[calc(100vw-2rem)] max-w-lg -translate-x-1/2 -translate-y-1/2 overflow-y-auto rounded-shell bg-bg p-6 text-fg shadow-overlay"
        >
          <div className="flex items-start justify-between gap-4">
            <Dialog.Title className="text-lg font-semibold">{channel ? `Edit ${channel.name}` : 'Add alert channel'}</Dialog.Title>
            <Dialog.Close className="btn btn-ghost btn-icon -mr-2 -mt-2" aria-label="Close">
              <X size={16} strokeWidth={1.5} aria-hidden />
            </Dialog.Close>
          </div>
          <ChannelForm
            channel={channel}
            onDone={(saved) => {
              onOpenChange(false)
              if (saved) onSaved?.(saved)
            }}
          />
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  )
}
