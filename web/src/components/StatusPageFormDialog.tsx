import * as Dialog from '@radix-ui/react-dialog'
import { X } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { ApiError } from '@/api/client'
import { useCreateStatusPage } from '@/data/statusPages'
import { slugify, slugProblem } from '@/lib/slug'
import { Notice } from './Notice'
import { TextField } from './TextField'

function CreateForm({ onDone }: { onDone: (id?: string) => void }) {
  const create = useCreateStatusPage()
  const [title, setTitle] = useState('')
  const [slug, setSlug] = useState('')
  const [slugEdited, setSlugEdited] = useState(false)
  const [errors, setErrors] = useState<{ title?: string; slug?: string }>({})

  const failure = create.error instanceof ApiError ? create.error : null
  const server = (field: string) => failure?.fields[field]?.[0] ?? failure?.fields[field.charAt(0).toUpperCase() + field.slice(1)]?.[0]
  const banner = failure && Object.keys(failure.fields).length === 0 ? failure.message : null

  const submit = (event: FormEvent) => {
    event.preventDefault()
    const found: typeof errors = {}
    if (!title.trim()) found.title = 'Enter a title'
    const problem = slugProblem(slug)
    if (problem) found.slug = problem
    setErrors(found)
    if (Object.keys(found).length > 0) return

    create.mutate(
      { title, slug, accent: '', footer: '', showResponseTimes: true },
      { onSuccess: (page) => onDone(page.id) },
    )
  }

  return (
    <form onSubmit={submit} noValidate className="mt-4 flex flex-col gap-5">
      {banner && <Notice tone="error">{banner}</Notice>}

      <TextField
        label="Title"
        placeholder="Acme status"
        value={title}
        error={errors.title ?? server('title')}
        onChange={(e) => {
          setTitle(e.target.value)
          if (!slugEdited) setSlug(slugify(e.target.value))
        }}
      />

      <TextField
        label="Address"
        placeholder="acme-status"
        autoCapitalize="none"
        spellCheck={false}
        hint={<>Your page will live at <span className="mono">/status/{slug || 'your-address'}</span></>}
        value={slug}
        error={errors.slug ?? server('slug')}
        onChange={(e) => {
          setSlugEdited(true)
          setSlug(e.target.value.toLowerCase())
        }}
      />

      <div className="flex justify-end gap-3">
        <button type="button" className="btn" onClick={() => onDone()}>Cancel</button>
        <button type="submit" className="btn btn-primary" disabled={create.isPending}>
          {create.isPending ? 'Creating…' : 'Create page'}
        </button>
      </div>
    </form>
  )
}

interface StatusPageFormDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  onCreated?: (id: string) => void
}

/** Asks only for a title and an address; everything else is set on the page's own screen afterwards. */
export function StatusPageFormDialog({ open, onOpenChange, onCreated }: StatusPageFormDialogProps) {
  return (
    <Dialog.Root open={open} onOpenChange={onOpenChange}>
      <Dialog.Portal>
        <Dialog.Overlay className="fixed inset-0 z-50 bg-black/50" />
        <Dialog.Content
          aria-describedby={undefined}
          className="fixed left-1/2 top-1/2 z-50 max-h-[calc(100dvh-2rem)] w-[calc(100vw-2rem)] max-w-lg -translate-x-1/2 -translate-y-1/2 overflow-y-auto rounded-shell bg-bg p-6 text-fg shadow-overlay"
        >
          <div className="flex items-start justify-between gap-4">
            <Dialog.Title className="text-lg font-semibold">Create a status page</Dialog.Title>
            <Dialog.Close className="btn btn-ghost btn-icon -mr-2 -mt-2" aria-label="Close">
              <X size={16} strokeWidth={1.5} aria-hidden />
            </Dialog.Close>
          </div>
          <CreateForm
            onDone={(id) => {
              onOpenChange(false)
              if (id) onCreated?.(id)
            }}
          />
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  )
}
