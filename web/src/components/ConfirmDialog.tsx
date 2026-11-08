import * as Dialog from '@radix-ui/react-dialog'
import type { ReactNode } from 'react'
import { Notice } from './Notice'

interface ConfirmDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  title: string
  description: ReactNode
  confirmLabel: string
  onConfirm: () => void
  pending?: boolean
  /** Why the last attempt failed, shown inside the dialog so it is not missed. */
  error?: string | null
  /** Extra content between the description and the buttons, for example a "type the name" field. */
  children?: ReactNode
  /** Disables the confirm button until the extra content is satisfied. */
  confirmDisabled?: boolean
}

/** A destructive action asks first, says what will happen, and names the button after it ("Remove Ana", not "OK"). */
export function ConfirmDialog({
  open,
  onOpenChange,
  title,
  description,
  confirmLabel,
  onConfirm,
  pending = false,
  error,
  children,
  confirmDisabled = false,
}: ConfirmDialogProps) {
  return (
    <Dialog.Root open={open} onOpenChange={onOpenChange}>
      <Dialog.Portal>
        <Dialog.Overlay className="fixed inset-0 z-50 bg-black/50" />
        <Dialog.Content className="fixed left-1/2 top-1/2 z-50 w-[calc(100vw-2rem)] max-w-md -translate-x-1/2 -translate-y-1/2 rounded-shell bg-bg p-6 text-fg shadow-overlay">
          <Dialog.Title className="text-lg font-semibold">{title}</Dialog.Title>
          <Dialog.Description asChild>
            <div className="mt-2 text-base text-muted">{description}</div>
          </Dialog.Description>
          {children && <div className="mt-4">{children}</div>}
          {error && <Notice tone="error" className="mt-4">{error}</Notice>}
          <div className="mt-6 flex justify-end gap-2">
            <Dialog.Close className="btn">Cancel</Dialog.Close>
            <button
              type="button"
              className="btn btn-dark"
              style={{ background: 'var(--status-down)', borderColor: 'var(--status-down)', color: '#fff' }}
              disabled={pending || confirmDisabled}
              onClick={onConfirm}
            >
              {pending ? 'Working…' : confirmLabel}
            </button>
          </div>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  )
}
