import type { StatusComponent } from '@/data/types'

export type OverallState = 'operational' | 'degraded' | 'partial' | 'major' | 'maintenance'

export interface Overall {
  state: OverallState
  headline: string
}

/**
 * The banner at the top of a status page, from its components' current states.
 * Every component down is a major outage; some down is partial; anything slow is degraded.
 */
export function overallStatus(components: Pick<StatusComponent, 'status'>[]): Overall {
  if (components.length === 0) return { state: 'operational', headline: 'All systems operational' }

  const down = components.filter((c) => c.status === 'down').length
  if (down === components.length) return { state: 'major', headline: 'Major outage' }
  if (down > 0) return { state: 'partial', headline: 'Partial outage' }
  if (components.some((c) => c.status === 'degraded')) return { state: 'degraded', headline: 'Degraded performance' }
  if (components.some((c) => c.status === 'maintenance')) return { state: 'maintenance', headline: 'Under maintenance' }
  return { state: 'operational', headline: 'All systems operational' }
}

/** Components grouped by their group name, keeping the order groups first appear in. */
export function groupComponents<T extends { group: string }>(components: T[]): [string, T[]][] {
  const groups = new Map<string, T[]>()
  for (const component of components) {
    const list = groups.get(component.group) ?? []
    list.push(component)
    groups.set(component.group, list)
  }
  return [...groups.entries()]
}
