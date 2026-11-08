import type { LucideIcon } from 'lucide-react'
import type { ReactNode } from 'react'

interface EmptyStateProps {
  icon: LucideIcon
  title: string
  description: string
  /** The one clear next step. */
  action?: ReactNode
}

export function EmptyState({ icon: Icon, title, description, action }: EmptyStateProps) {
  return (
    <div className="card flex flex-col items-start gap-4 p-8">
      <span className="flex size-8 items-center justify-center rounded-control border border-border-strong bg-surface-2 text-muted">
        <Icon size={16} strokeWidth={1.5} aria-hidden />
      </span>
      <div className="max-w-md">
        <h2 className="text-md text-fg">{title}</h2>
        <p className="mt-1 text-base text-muted">{description}</p>
      </div>
      {action}
    </div>
  )
}
