import type { LucideIcon } from 'lucide-react'

export type CommandGroup = 'Monitors' | 'Go to' | 'Actions'

export interface Command {
  id: string
  group: CommandGroup
  label: string
  /** Secondary text, such as a monitor's address. */
  hint?: string
  /** Extra words that should find this command without being shown. */
  keywords?: string
  icon: LucideIcon
  run: () => void
}

export const GROUP_ORDER: CommandGroup[] = ['Go to', 'Actions', 'Monitors']

/** Every word typed must appear somewhere in the command's text, in any order. */
export function matches(command: Command, query: string): boolean {
  const terms = query.toLowerCase().split(/\s+/).filter(Boolean)
  if (terms.length === 0) return true
  const text = `${command.label} ${command.hint ?? ''} ${command.keywords ?? ''}`.toLowerCase()
  return terms.every((term) => text.includes(term))
}

/** Commands that match, kept in group order and, within a group, with label matches ahead of the rest. */
export function filterCommands(commands: Command[], query: string): Command[] {
  const first = query.trim().toLowerCase().split(/\s+/)[0] ?? ''
  const found = commands.filter((command) => matches(command, query))
  const rank = (command: Command) => {
    const group = GROUP_ORDER.indexOf(command.group)
    const label = first && command.label.toLowerCase().startsWith(first) ? 0 : 1
    return group * 2 + label
  }
  return found
    .map((command, index) => ({ command, index }))
    .sort((a, b) => rank(a.command) - rank(b.command) || a.index - b.index)
    .map(({ command }) => command)
}
