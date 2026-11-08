import * as Dialog from '@radix-ui/react-dialog'
import { X } from 'lucide-react'
import { useId, useState, type FormEvent, type ReactNode } from 'react'
import { ApiError } from '@/api/client'
import { useDeclareIncident } from '@/data/incidents'
import { SEVERITY_LABEL, STATUS_LABEL } from '@/data/incidentView'
import { useMonitorList } from '@/data/monitors'
import { useStatusPages } from '@/data/statusPages'
import type { IncidentStatus, Severity } from '@/data/types'
import { Notice } from './Notice'
import { TextField } from './TextField'

const SEVERITIES: Severity[] = ['degraded', 'partial', 'major', 'maintenance']
const STATUSES: IncidentStatus[] = ['investigating', 'identified', 'monitoring']

function Field({ label, error, children }: { label: string; error?: string; children: (id: string, describedBy?: string) => ReactNode }) {
  const id = useId()
  return (
    <div className="field">
      <label htmlFor={id} className="field-label">{label}</label>
      {children(id, error ? `${id}-error` : undefined)}
      {error && <p id={`${id}-error`} className="field-error" role="alert">{error}</p>}
    </div>
  )
}

function DeclareForm({ onDone }: { onDone: (id?: string) => void }) {
  const declare = useDeclareIncident()
  const monitors = useMonitorList()
  const pages = useStatusPages()
  const [componentIds, setComponentIds] = useState<string[]>([])
  const [title, setTitle] = useState('')
  const [severity, setSeverity] = useState<Severity>('degraded')
  const [status, setStatus] = useState<IncidentStatus>('investigating')
  const [message, setMessage] = useState('')
  const [monitorId, setMonitorId] = useState('')
  const [errors, setErrors] = useState<{ title?: string; message?: string }>({})

  const failure = declare.error instanceof ApiError ? declare.error : null
  const serverField = (name: string) => failure?.fields[name]?.[0] ?? failure?.fields[name.charAt(0).toUpperCase() + name.slice(1)]?.[0]
  const banner = failure && Object.keys(failure.fields).length === 0 ? failure.message : null

  const submit = (event: FormEvent) => {
    event.preventDefault()
    const found: typeof errors = {}
    if (!title.trim()) found.title = 'Enter a title'
    if (!message.trim()) found.message = 'Say what is happening'
    setErrors(found)
    if (Object.keys(found).length > 0) return

    declare.mutate(
      { title: title.trim(), severity, status, message: message.trim(), monitorId: monitorId || null, componentIds },
      { onSuccess: (detail) => onDone(detail.incident.id) },
    )
  }

  return (
    <form onSubmit={submit} noValidate className="mt-4 flex flex-col gap-5">
      {banner && <Notice tone="error">{banner}</Notice>}

      <TextField
        label="Title"
        placeholder="Checkout is slow for some customers"
        value={title}
        error={errors.title ?? serverField('title')}
        onChange={(e) => setTitle(e.target.value)}
      />

      <div className="grid gap-5 sm:grid-cols-2">
        <Field label="Severity">
          {(id) => (
            <select id={id} className="input" value={severity} onChange={(e) => setSeverity(e.target.value as Severity)}>
              {SEVERITIES.map((s) => <option key={s} value={s}>{SEVERITY_LABEL[s]}</option>)}
            </select>
          )}
        </Field>
        <Field label="Status">
          {(id) => (
            <select id={id} className="input" value={status} onChange={(e) => setStatus(e.target.value as IncidentStatus)}>
              {STATUSES.map((s) => <option key={s} value={s}>{STATUS_LABEL[s]}</option>)}
            </select>
          )}
        </Field>
      </div>

      <Field label="What is happening" error={errors.message ?? serverField('message')}>
        {(id, describedBy) => (
          <textarea
            id={id}
            className="input min-h-24 py-3"
            aria-invalid={errors.message || serverField('message') ? true : undefined}
            aria-describedby={describedBy}
            value={message}
            onChange={(e) => setMessage(e.target.value)}
          />
        )}
      </Field>

      {(monitors.data?.length ?? 0) > 0 && (
        <Field label="Related monitor (optional)">
          {(id) => (
            <select id={id} className="input" value={monitorId} onChange={(e) => setMonitorId(e.target.value)}>
              <option value="">None</option>
              {monitors.data?.map((m) => <option key={m.id} value={m.id}>{m.name}</option>)}
            </select>
          )}
        </Field>
      )}

      {(pages.data?.some((page) => page.components.length > 0) ?? false) && (
        <fieldset className="field">
          <legend className="field-label">Show on your status pages (optional)</legend>
          <p className="field-hint mb-1">Pick the components customers will see this under. Subscribers of those pages get an email for each public update.</p>
          <div className="flex max-h-48 flex-col gap-3 overflow-y-auto">
            {pages.data
              ?.filter((page) => page.components.length > 0)
              .map((page) => (
                <div key={page.id}>
                  <p className="text-xs font-medium text-muted">{page.title}</p>
                  <ul className="flex flex-col gap-1">
                    {page.components.map((component) => (
                      <li key={component.id}>
                        <label className="flex min-h-9 cursor-pointer items-center gap-3 text-base">
                          <input
                            type="checkbox"
                            className="size-4 accent-[var(--brand-ink)]"
                            checked={componentIds.includes(component.id)}
                            onChange={() =>
                              setComponentIds((list) => (list.includes(component.id) ? list.filter((id) => id !== component.id) : [...list, component.id]))
                            }
                          />
                          <span className="min-w-0 truncate">{component.name}</span>
                        </label>
                      </li>
                    ))}
                  </ul>
                </div>
              ))}
          </div>
        </fieldset>
      )}

      <div className="flex justify-end gap-3">
        <button type="button" className="btn" onClick={() => onDone()}>Cancel</button>
        <button type="submit" className="btn btn-primary" disabled={declare.isPending}>
          {declare.isPending ? 'Posting…' : 'Declare incident'}
        </button>
      </div>
    </form>
  )
}

interface DeclareIncidentDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  onDeclared?: (id: string) => void
}

/** For things the monitors cannot see: a planned maintenance, a partner outage, a slow dashboard. */
export function DeclareIncidentDialog({ open, onOpenChange, onDeclared }: DeclareIncidentDialogProps) {
  return (
    <Dialog.Root open={open} onOpenChange={onOpenChange}>
      <Dialog.Portal>
        <Dialog.Overlay className="fixed inset-0 z-50 bg-black/50" />
        <Dialog.Content
          aria-describedby={undefined}
          className="fixed left-1/2 top-1/2 z-50 max-h-[calc(100dvh-2rem)] w-[calc(100vw-2rem)] max-w-lg -translate-x-1/2 -translate-y-1/2 overflow-y-auto rounded-shell bg-bg p-6 text-fg shadow-overlay"
        >
          <div className="flex items-start justify-between gap-4">
            <Dialog.Title className="text-lg font-semibold">Declare an incident</Dialog.Title>
            <Dialog.Close className="btn btn-ghost btn-icon -mr-2 -mt-2" aria-label="Close">
              <X size={16} strokeWidth={1.5} aria-hidden />
            </Dialog.Close>
          </div>
          <p className="mt-1 text-sm text-muted">Use this for problems the monitors cannot see. Each update you post is added to the incident timeline.</p>
          <DeclareForm
            onDone={(id) => {
              onOpenChange(false)
              if (id) onDeclared?.(id)
            }}
          />
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  )
}
