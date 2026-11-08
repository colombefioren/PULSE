import type { ReactNode } from 'react'

interface PageHeaderProps {
  title: string
  description?: string
  actions?: ReactNode
}

export function PageHeader({ title, description, actions }: PageHeaderProps) {
  return (
    <header className="mb-6 flex flex-wrap items-start justify-between gap-4">
      <div>
        <h1 className="text-xl text-fg">{title}</h1>
        {description && <p className="mt-1 text-base text-muted">{description}</p>}
      </div>
      {actions}
    </header>
  )
}
