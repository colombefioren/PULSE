import * as Dialog from '@radix-ui/react-dialog'
import { X } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { ApiError } from '@/api/client'
import { useCreateOrg } from '@/api/hooks'
import { validateName } from '@/lib/validation'
import { useSession } from '@/session/useSession'
import { Notice } from './Notice'
import { TextField } from './TextField'

export function CreateTeamDialog({ open, onOpenChange }: { open: boolean; onOpenChange: (open: boolean) => void }) {
  const session = useSession()
  const create = useCreateOrg()
  const [name, setName] = useState('')
  const [touched, setTouched] = useState(false)

  const error = touched ? validateName(name, 'a team name') : null
  const failure = create.error instanceof ApiError ? create.error : null

  const close = (next: boolean) => {
    if (!next) {
      setName('')
      setTouched(false)
      create.reset()
    }
    onOpenChange(next)
  }

  const submit = (event: FormEvent) => {
    event.preventDefault()
    setTouched(true)
    if (validateName(name, 'a team name') !== null) return
    create.mutate(name.trim(), {
      onSuccess: (org) => {
        if (session.status === 'ready') session.selectOrg(org.id)
        close(false)
      },
    })
  }

  return (
    <Dialog.Root open={open} onOpenChange={close}>
      <Dialog.Portal>
        <Dialog.Overlay className="fixed inset-0 z-50 bg-black/50" />
        <Dialog.Content
          aria-describedby={undefined}
          className="fixed left-1/2 top-1/2 z-50 w-[calc(100vw-2rem)] max-w-md -translate-x-1/2 -translate-y-1/2 rounded-shell bg-bg p-6 text-fg shadow-overlay"
        >
          <div className="flex items-start justify-between gap-4">
            <Dialog.Title className="text-lg font-semibold">New team</Dialog.Title>
            <Dialog.Close className="btn btn-ghost btn-icon -mr-2 -mt-2" aria-label="Close">
              <X size={16} strokeWidth={1.5} aria-hidden />
            </Dialog.Close>
          </div>
          <form onSubmit={submit} noValidate className="mt-4 flex flex-col gap-5">
            {failure && <Notice tone="error">{failure.fields.name?.[0] ?? failure.message}</Notice>}
            <TextField
              label="Team name"
              autoComplete="organization"
              placeholder="Acme Platform"
              hint="Monitors, status pages and members belong to a team. You will be its Owner."
              value={name}
              error={error}
              onChange={(event) => setName(event.target.value)}
              onBlur={() => setTouched(true)}
            />
            <button type="submit" className="btn btn-primary btn-lg w-full" disabled={create.isPending}>
              {create.isPending ? 'Creating…' : 'Create team'}
            </button>
          </form>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  )
}
