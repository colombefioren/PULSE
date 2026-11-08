import { describe, expect, it } from 'vitest'
import { groupComponents, overallStatus } from './overall'

const c = (status: 'up' | 'degraded' | 'down' | 'maintenance') => ({ status })

describe('overallStatus', () => {
  it('is operational when everything is up, and when there is nothing yet', () => {
    expect(overallStatus([c('up'), c('up')]).headline).toBe('All systems operational')
    expect(overallStatus([]).state).toBe('operational')
  })

  it('is a partial outage when some components are down', () => {
    expect(overallStatus([c('up'), c('down')])).toEqual({ state: 'partial', headline: 'Partial outage' })
  })

  it('is a major outage when every component is down', () => {
    expect(overallStatus([c('down'), c('down')]).state).toBe('major')
  })

  it('ranks an outage above degraded performance', () => {
    expect(overallStatus([c('degraded'), c('down'), c('up')]).state).toBe('partial')
    expect(overallStatus([c('degraded'), c('up')]).state).toBe('degraded')
  })

  it('reports maintenance when that is the only thing going on', () => {
    expect(overallStatus([c('maintenance'), c('up')]).state).toBe('maintenance')
  })
})

describe('groupComponents', () => {
  it('keeps groups in the order they first appear', () => {
    const grouped = groupComponents([
      { group: 'Core', id: 1 },
      { group: 'Web', id: 2 },
      { group: 'Core', id: 3 },
    ])

    expect(grouped.map(([name]) => name)).toEqual(['Core', 'Web'])
    expect(grouped[0]?.[1].map((x) => x.id)).toEqual([1, 3])
  })
})
