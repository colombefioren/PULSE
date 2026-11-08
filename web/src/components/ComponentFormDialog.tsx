import * as Dialog from '@radix-ui/react-dialog'
import { X } from 'lucide-react'
import { useId, useState, type FormEvent } from 'react'
import { Link } from 'react-router'
import { ApiError } from '@/api/client'
import { useMonitorList } from '@/data/monitors'
import { useSaveComponent, type ComponentView } from '@/data/statusPages'
import { Notice } from './Notice'
import { TextField } from './TextField'

interface FormProps {
  pageId: string
  component?: ComponentView
  /** Groups already used on the page, offered as suggestions. */
  groups: string[]
  onDone: () => void
}

function ComponentForm({ pageId, component, groups, onDone }: FormProps) {
  const save = useSaveComponent(pageId)
  const monitors = useMonitorList()
  const listId = useId()
  const [name, setName] = useState(component?.name ?? '')
  const [group, setGroup] = useState(component?.group ?? '')
  const [selected, setSelected] = useState<string[]>(component?.monitorIds ?? [])
  const [errors, setErrors] = useState<{ name?: string; monitors?: string }>({})

  const failure = save.error instanceof ApiError ? save.error : null
  const server = (field: string) => failure?.fields[field]?.[0] ?? failure?.fields[field.charAt(0).toUpperCase() + field.slice(1)]?.[0]
  const banner = failure && Object.keys(failure.fields).length === 0 ? failure.message : null

  const toggle = (id: string) => setSelected((list) => (list.includes(id) ? list.filter((x) => x !== id) : [...list, id]))

  const submit = (event: FormEvent) => {
    event.preventDefault()
    const found: typeof errors = {}
    if (!name.trim()) found.name = 'Enter a name'
    if (selected.length === 0) found.monitors = 'Choose at least one monitor'
    setErrors(found)
    if (Object.keys(found).length > 0) return

    save.mutate({ id: component?.id, draft: { name, group, monitorIds: selected } }, { onSuccess: () => onDone() })
  }

  const monitorError = errors.monitors ?? server('monitorIds')

  return (
    <form onSubmit={submit} noValidate className="mt-4 flex flex-col gap-5">
      {banner && <Notice tone="error">{banner}</Notice>}

      <TextField label="Name" placeholder="Website" value={name} error={errors.name ?? server('name')} onChange={(e) => setName(e.target.value)} />

      <div className="field">
        <label htmlFor={`${listId}-group`} className="field-label">Group (optional)</label>
        <input
          id={`${listId}-group`}
          className="input"
          list={`${listId}-groups`}
          placeholder="Core services"
          value={group}
          onChange={(e) => setGroup(e.target.value)}
        />
        <datalist id={`${listId}-groups`}>
          {groups.map((g) => <option key={g} value={g} />)}
        </datalist>
        <p className="field-hint">Components in the same group are listed together on the page.</p>
      </div>

      <fieldset className="field" aria-describedby={monitorError ? `${listId}-monitors-error` : undefined}>
        <legend className="field-label">Monitors behind this component</legend>
        {monitors.isPending ? (
          <div className="skeleton h-16" aria-hidden />
        ) : (monitors.data?.length ?? 0) === 0 ? (
          <p className="field-hint">
            You have no monitors yet. <Link to="/monitors" className="underline" onClick={() => onDone()}>Add one</Link> first.
          </p>
        ) : (
          <ul className="flex max-h-56 flex-col gap-1 overflow-y-auto">
            {monitors.data?.map((monitor) => (
              <li key={monitor.id}>
                <label className="flex min-h-9 cursor-pointer items-center gap-3 text-base">
                  <input
                    type="checkbox"
                    className="size-4 accent-[var(--brand-ink)]"
                    checked={selected.includes(monitor.id)}
                    onChange={() => toggle(monitor.id)}
                  />
                  <span className="min-w-0 truncate">{monitor.name}</span>
                </label>
              </li>
            ))}
          </ul>
        )}
        {monitorError && <p id={`${listId}-monitors-error`} className="field-error" role="alert">{monitorError}</p>}
        <p className="field-hint">The component is down when all of them are down, and degraded when only some are.</p>
      </fieldset>

      <div className="flex justify-end gap-3">
        <button type="button" className="btn" onClick={() => onDone()}>Cancel</button>
        <button type="submit" className="btn btn-primary" disabled={save.isPending}>
          {save.isPending ? 'Saving…' : component ? 'Save changes' : 'Add component'}
        </button>
      </div>
    </form>
  )
}

interface ComponentFormDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  pageId: string
  /** The component being edited; leave out to add one. */
  component?: ComponentView
  groups: string[]
}

export function ComponentFormDialog({ open, onOpenChange, pageId, component, groups }: ComponentFormDialogProps) {
  return (
    <Dialog.Root open={open} onOpenChange={onOpenChange}>
      <Dialog.Portal>
        <Dialog.Overlay className="fixed inset-0 z-50 bg-black/50" />
        <Dialog.Content
          aria-describedby={undefined}
          className="fixed left-1/2 top-1/2 z-50 max-h-[calc(100dvh-2rem)] w-[calc(100vw-2rem)] max-w-lg -translate-x-1/2 -translate-y-1/2 overflow-y-auto rounded-shell bg-bg p-6 text-fg shadow-overlay"
        >
          <div className="flex items-start justify-between gap-4">
            <Dialog.Title className="text-lg font-semibold">{component ? `Edit ${component.name}` : 'Add a component'}</Dialog.Title>
            <Dialog.Close className="btn btn-ghost btn-icon -mr-2 -mt-2" aria-label="Close">
              <X size={16} strokeWidth={1.5} aria-hidden />
            </Dialog.Close>
          </div>
          <ComponentForm pageId={pageId} component={component} groups={groups} onDone={() => onOpenChange(false)} />
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  )
}
