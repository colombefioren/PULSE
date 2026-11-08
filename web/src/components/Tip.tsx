import * as Tooltip from '@radix-ui/react-tooltip'
import type { ReactNode } from 'react'

interface TipProps {
  label: string
  /** Keys shown after the label, e.g. ['G', 'M']. */
  keys?: string[]
  side?: 'top' | 'right' | 'bottom' | 'left'
  children: ReactNode
}

/** Tooltip with an optional keyboard shortcut. The trigger must be a focusable element. */
export function Tip({ label, keys, side = 'bottom', children }: TipProps) {
  return (
    <Tooltip.Root delayDuration={300}>
      <Tooltip.Trigger asChild>{children}</Tooltip.Trigger>
      <Tooltip.Portal>
        <Tooltip.Content
          side={side}
          sideOffset={6}
          className="z-50 flex items-center gap-2 rounded-control border border-border-strong bg-surface px-2 py-1 text-xs text-fg shadow-overlay"
        >
          {label}
          {keys && (
            <span className="flex gap-1">
              {keys.map((key) => (
                <kbd key={key} className="kbd">
                  {key}
                </kbd>
              ))}
            </span>
          )}
        </Tooltip.Content>
      </Tooltip.Portal>
    </Tooltip.Root>
  )
}
