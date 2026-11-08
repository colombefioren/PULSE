import type { ReactNode } from 'react'

interface NoticeProps {
  tone: 'error' | 'success'
  children: ReactNode
  /** Use alert for something that just went wrong; status for something that just worked. */
  className?: string
}

export function Notice({ tone, children, className = '' }: NoticeProps) {
  const palette =
    tone === 'error'
      ? 'border-down/30 bg-down/10 text-fg'
      : 'border-up/30 bg-up/10 text-fg'
  return (
    <div role={tone === 'error' ? 'alert' : 'status'} className={`rounded-2xl border px-4 py-3 text-sm ${palette} ${className}`}>
      {children}
    </div>
  )
}
