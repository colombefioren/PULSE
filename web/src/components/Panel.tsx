import type { ReactNode } from 'react'

interface PanelProps {
  title: string
  /** Right-aligned control or link in the header. */
  action?: ReactNode
  children: ReactNode
  className?: string
  /** Stagger index for the entrance animation. */
  index?: number
}

export function Panel({ title, action, children, className = '', index = 0 }: PanelProps) {
  return (
    <section className={`card rise p-5 ${className}`} style={{ '--i': index } as React.CSSProperties}>
      <header className="mb-4 flex items-center justify-between gap-3">
        <h2 className="text-md text-fg">{title}</h2>
        {action}
      </header>
      {children}
    </section>
  )
}
