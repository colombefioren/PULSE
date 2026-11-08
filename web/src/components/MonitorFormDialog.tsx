import * as Dialog from '@radix-ui/react-dialog'
import { X } from 'lucide-react'
import { useId, useState, type FormEvent, type ReactNode } from 'react'
import { ApiError } from '@/api/client'
import type { ApiMethod, MonitorInput } from '@/api/types'
import { Link } from 'react-router'
import { useChannels } from '@/data/alerts'
import { useSaveMonitor } from '@/data/monitors'
import { headersToText, inputFromView, NEW_MONITOR, parseHeaders, type MonitorView } from '@/data/monitorView'
import { CHANNEL_LABEL } from '@/data/incidentView'
import { Notice } from './Notice'
import { TextField } from './TextField'

const INTERVALS: [seconds: number, label: string][] = [
  [30, 'Every 30 seconds'],
  [60, 'Every minute'],
  [300, 'Every 5 minutes'],
  [600, 'Every 10 minutes'],
  [1800, 'Every 30 minutes'],
]

type FieldName = 'name' | 'url' | 'timeout' | 'codes' | 'keyword' | 'headers' | 'failure' | 'recovery'
type Errors = Partial<Record<FieldName, string>>

/** The API names fields after its JSON properties; the form names them after what people see. */
const SERVER_FIELDS: Record<string, FieldName> = {
  name: 'name',
  url: 'url',
  timeoutMs: 'timeout',
  intervalSeconds: 'timeout',
  expectedCodes: 'codes',
  keyword: 'keyword',
  headers: 'headers',
  failureThreshold: 'failure',
  recoveryThreshold: 'recovery',
}

function Field({ label, hint, error, children }: { label: string; hint?: ReactNode; error?: string; children: (id: string, describedBy?: string) => ReactNode }) {
  const id = useId()
  const describedBy = error ? `${id}-error` : hint ? `${id}-hint` : undefined
  return (
    <div className="field">
      <label htmlFor={id} className="field-label">{label}</label>
      {children(id, describedBy)}
      {error ? (
        <p id={`${id}-error`} className="field-error" role="alert">{error}</p>
      ) : hint ? (
        <p id={`${id}-hint`} className="field-hint">{hint}</p>
      ) : null}
    </div>
  )
}

function MonitorForm({ monitor, onDone }: { monitor?: MonitorView; onDone: (savedId?: string) => void }) {
  const save = useSaveMonitor(monitor?.id)
  const initial: MonitorInput = monitor ? inputFromView(monitor) : NEW_MONITOR

  const [name, setName] = useState(initial.name)
  const [url, setUrl] = useState(initial.url)
  const [method, setMethod] = useState<ApiMethod>(initial.method)
  const [interval, setIntervalSeconds] = useState(initial.intervalSeconds)
  const [timeout, setTimeoutSeconds] = useState(String(initial.timeoutMs / 1000))
  const [codes, setCodes] = useState(initial.expectedCodes ?? '')
  const [keyword, setKeyword] = useState(initial.keyword ?? '')
  const [headers, setHeaders] = useState(headersToText(initial.headers))
  const [failure, setFailure] = useState(String(initial.failureThreshold))
  const [recovery, setRecovery] = useState(String(initial.recoveryThreshold))
  const [errors, setErrors] = useState<Errors>({})
  const channels = useChannels()
  // Until someone chooses, a new monitor alerts every channel and an existing one keeps what it had.
  const [chosen, setChosen] = useState<string[] | null>(null)
  const selectedChannels = chosen ?? (monitor ? monitor.alertChannelIds : (channels.data ?? []).map((c) => c.id))
  const toggleChannel = (id: string) =>
    setChosen(selectedChannels.includes(id) ? selectedChannels.filter((x) => x !== id) : [...selectedChannels, id])

  const failureError = save.error instanceof ApiError ? save.error : null
  const unplaced = failureError
    ? Object.keys(failureError.fields).filter((key) => !SERVER_FIELDS[key.charAt(0).toLowerCase() + key.slice(1)])
    : []

  const submit = (event: FormEvent) => {
    event.preventDefault()
    const found: Errors = {}
    const seconds = Number(timeout)
    const failures = Number(failure)
    const recoveries = Number(recovery)
    const parsed = parseHeaders(headers)

    if (!name.trim()) found.name = 'Enter a name'
    if (!url.trim()) found.url = 'Enter the address to check, like https://example.com/health'
    if (!Number.isInteger(seconds) || seconds < 1 || seconds > 30) found.timeout = 'Use a whole number of seconds from 1 to 30'
    else if (seconds > interval) found.timeout = 'The timeout cannot be longer than the check interval'
    if (method === 'Head' && keyword) found.keyword = 'A HEAD request has no body to search. Use GET or POST to check for a keyword'
    if (parsed.error) found.headers = parsed.error
    if (!Number.isInteger(failures) || failures < 1 || failures > 10) found.failure = 'Use a number from 1 to 10'
    if (!Number.isInteger(recoveries) || recoveries < 1 || recoveries > 10) found.recovery = 'Use a number from 1 to 10'

    setErrors(found)
    if (Object.keys(found).length > 0) return

    save.mutate(
      {
        name: name.trim(),
        url: url.trim(),
        method,
        intervalSeconds: interval,
        timeoutMs: seconds * 1000,
        expectedCodes: codes.trim() || null,
        keyword: keyword || null,
        headers: parsed.headers,
        failureThreshold: failures,
        recoveryThreshold: recoveries,
        alertChannelIds: selectedChannels,
      },
      { onSuccess: (saved) => onDone(saved.id) },
    )
  }

  // Server messages win over local ones: they know about rules the form does not (private addresses, DNS).
  const shown: Errors = { ...errors }
  if (failureError) {
    for (const [key, messages] of Object.entries(failureError.fields)) {
      const field = SERVER_FIELDS[key.charAt(0).toLowerCase() + key.slice(1)]
      if (field && messages[0]) shown[field] = messages[0]
    }
  }

  const bannerMessage = failureError && (Object.keys(failureError.fields).length === 0 || unplaced.length > 0) ? failureError.message : null

  return (
    <form onSubmit={submit} noValidate className="mt-4 flex flex-col gap-5">
      {bannerMessage && <Notice tone="error">{bannerMessage}</Notice>}

      <TextField label="Name" placeholder="Marketing site" value={name} error={shown.name} onChange={(e) => setName(e.target.value)} />
      <TextField
        label="URL"
        type="url"
        inputMode="url"
        autoCapitalize="none"
        spellCheck={false}
        placeholder="https://example.com/health"
        hint="Public http or https address. Internal and private addresses can't be checked."
        value={url}
        error={shown.url}
        onChange={(e) => setUrl(e.target.value)}
      />

      <div className="grid gap-5 sm:grid-cols-2">
        <Field label="Method">
          {(id) => (
            <select id={id} className="input" value={method} onChange={(e) => setMethod(e.target.value as ApiMethod)}>
              <option value="Get">GET</option>
              <option value="Head">HEAD</option>
              <option value="Post">POST</option>
            </select>
          )}
        </Field>
        <Field label="Check">
          {(id) => (
            <select id={id} className="input" value={interval} onChange={(e) => setIntervalSeconds(Number(e.target.value))}>
              {INTERVALS.map(([seconds, label]) => (
                <option key={seconds} value={seconds}>{label}</option>
              ))}
            </select>
          )}
        </Field>
      </div>

      {channels.data && (
        <fieldset className="field">
          <legend className="field-label">Send alerts to</legend>
          {channels.data.length === 0 ? (
            <p className="field-hint">
              No alert channels yet. <Link to="/alerts" className="underline" onClick={() => onDone()}>Add one</Link> to hear about outages by email, Slack or Discord.
            </p>
          ) : (
            <ul className="flex flex-col gap-1">
              {channels.data.map((channel) => (
                <li key={channel.id}>
                  <label className="flex min-h-9 cursor-pointer items-center gap-3 text-base">
                    <input
                      type="checkbox"
                      className="size-4 accent-[var(--brand-ink)]"
                      checked={selectedChannels.includes(channel.id)}
                      onChange={() => toggleChannel(channel.id)}
                    />
                    <span className="min-w-0 truncate">{channel.name}</span>
                    <span className="text-xs text-muted">{CHANNEL_LABEL[channel.type]}</span>
                  </label>
                </li>
              ))}
            </ul>
          )}
        </fieldset>
      )}

      <details className="rounded-2xl border border-border px-4 py-3" open={Boolean(monitor && (codes || keyword || headers))}>
        <summary className="cursor-pointer text-sm font-medium">Advanced</summary>
        <div className="mt-4 flex flex-col gap-5">
          <div className="grid gap-5 sm:grid-cols-2">
            <TextField
              label="Timeout (seconds)"
              inputMode="numeric"
              value={timeout}
              error={shown.timeout}
              onChange={(e) => setTimeoutSeconds(e.target.value)}
            />
            <TextField
              label="Expected status codes"
              placeholder="200-299"
              hint="Ranges and lists, like 200-299, 301."
              value={codes}
              error={shown.codes}
              onChange={(e) => setCodes(e.target.value)}
            />
          </div>
          <TextField
            label="Response must contain"
            placeholder="optional text"
            hint="Case-sensitive, searched in the first 1 MB of the response."
            value={keyword}
            error={shown.keyword}
            onChange={(e) => setKeyword(e.target.value)}
          />
          <Field label="Request headers" hint="One per line, like Authorization: Bearer abc. Up to 10." error={shown.headers}>
            {(id, describedBy) => (
              <textarea
                id={id}
                className="input mono min-h-24 py-3"
                spellCheck={false}
                autoCapitalize="none"
                aria-invalid={shown.headers ? true : undefined}
                aria-describedby={describedBy}
                value={headers}
                onChange={(e) => setHeaders(e.target.value)}
              />
            )}
          </Field>
          <div className="grid gap-5 sm:grid-cols-2">
            <TextField
              label="Down after (failed checks)"
              inputMode="numeric"
              hint="In a row, before the monitor is marked down."
              value={failure}
              error={shown.failure}
              onChange={(e) => setFailure(e.target.value)}
            />
            <TextField
              label="Up after (passed checks)"
              inputMode="numeric"
              hint="In a row, before it is marked up again."
              value={recovery}
              error={shown.recovery}
              onChange={(e) => setRecovery(e.target.value)}
            />
          </div>
        </div>
      </details>

      <div className="flex justify-end gap-3">
        <button type="button" className="btn" onClick={() => onDone()}>Cancel</button>
        <button type="submit" className="btn btn-primary" disabled={save.isPending}>
          {save.isPending ? 'Saving…' : monitor ? 'Save changes' : 'Add monitor'}
        </button>
      </div>
    </form>
  )
}

interface MonitorFormDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  /** The monitor being edited; leave out to add a new one. */
  monitor?: MonitorView
  onSaved?: (id: string) => void
}

export function MonitorFormDialog({ open, onOpenChange, monitor, onSaved }: MonitorFormDialogProps) {
  return (
    <Dialog.Root open={open} onOpenChange={onOpenChange}>
      <Dialog.Portal>
        <Dialog.Overlay className="fixed inset-0 z-50 bg-black/50" />
        <Dialog.Content
          aria-describedby={undefined}
          className="fixed left-1/2 top-1/2 z-50 max-h-[calc(100dvh-2rem)] w-[calc(100vw-2rem)] max-w-lg -translate-x-1/2 -translate-y-1/2 overflow-y-auto rounded-shell bg-bg p-6 text-fg shadow-overlay"
        >
          <div className="flex items-start justify-between gap-4">
            <Dialog.Title className="text-lg font-semibold">{monitor ? `Edit ${monitor.name}` : 'Add monitor'}</Dialog.Title>
            <Dialog.Close className="btn btn-ghost btn-icon -mr-2 -mt-2" aria-label="Close">
              <X size={16} strokeWidth={1.5} aria-hidden />
            </Dialog.Close>
          </div>
          {monitor && (
            <p className="mt-1 text-sm text-muted">Changing the address, method, status codes, keyword or headers starts the history over.</p>
          )}
          <MonitorForm
            monitor={monitor}
            onDone={(savedId) => {
              onOpenChange(false)
              if (savedId) onSaved?.(savedId)
            }}
          />
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  )
}
