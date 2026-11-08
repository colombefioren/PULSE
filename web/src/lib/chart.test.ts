import { describe, expect, it } from 'vitest'
import { areaPath, downtimeBands, linePath, makeScales, nearestIndex, niceMax, runs, type Box } from './chart'
import type { SeriesPoint } from './stats'

const box: Box = { width: 400, height: 200, left: 40, right: 0, top: 10, bottom: 20 }

const series: SeriesPoint[] = [
  { t: 0, ms: 100 },
  { t: 10, ms: 200 },
  { t: 20, ms: null },
  { t: 30, ms: null },
  { t: 40, ms: 100 },
]

describe('niceMax', () => {
  it('rounds up to a tidy ceiling', () => {
    expect(niceMax(87)).toBe(100)
    expect(niceMax(240)).toBe(250)
    expect(niceMax(1320)).toBe(1500)
    expect(niceMax(100)).toBe(100)
  })

  it('has a sane default for no data', () => {
    expect(niceMax(0)).toBe(100)
  })
})

describe('makeScales', () => {
  const scales = makeScales(series, box, 200)

  it('maps the first and last timestamps to the plot edges', () => {
    expect(scales.x(0)).toBe(40)
    expect(scales.x(40)).toBe(400)
  })

  it('puts zero on the baseline and the maximum on the top edge', () => {
    expect(scales.y(0)).toBe(180)
    expect(scales.y(200)).toBe(10)
  })

  it('clamps values above the axis maximum instead of drawing outside the plot', () => {
    expect(scales.y(900)).toBe(10)
  })
})

describe('runs', () => {
  it('splits the line wherever a check failed', () => {
    const result = runs(series)

    expect(result).toHaveLength(2)
    expect(result[0]?.map((p) => p.t)).toEqual([0, 10])
    expect(result[1]?.map((p) => p.t)).toEqual([40])
  })

  it('returns nothing when every check failed', () => {
    expect(runs([{ t: 0, ms: null }])).toEqual([])
  })
})

describe('paths', () => {
  const scales = makeScales(series, box, 200)

  it('draws a line through each point of a run', () => {
    expect(linePath(runs(series)[0] ?? [], scales)).toBe('M40 95L130 10')
  })

  it('closes an area down to the baseline', () => {
    const area = areaPath(runs(series)[0] ?? [], scales, 180)
    expect(area.startsWith('M40 95')).toBe(true)
    expect(area.endsWith('Z')).toBe(true)
  })
})

describe('downtimeBands', () => {
  it('marks the span of consecutive failed checks', () => {
    const scales = makeScales(series, box, 200)
    const [band, ...rest] = downtimeBands(series, scales)

    expect(rest).toHaveLength(0)
    expect(band?.from).toBeCloseTo(scales.x(10))
    expect(band?.to).toBeCloseTo(scales.x(40))
  })

  it('has no bands when nothing failed', () => {
    const ok: SeriesPoint[] = [
      { t: 0, ms: 1 },
      { t: 1, ms: 2 },
    ]
    expect(downtimeBands(ok, makeScales(ok, box))).toEqual([])
  })

  it('closes a band that runs to the end of the series', () => {
    const open: SeriesPoint[] = [
      { t: 0, ms: 1 },
      { t: 10, ms: null },
      { t: 20, ms: null },
    ]
    const scales = makeScales(open, box)

    expect(downtimeBands(open, scales)).toHaveLength(1)
  })
})

describe('nearestIndex', () => {
  it('finds the point closest to the pointer', () => {
    const scales = makeScales(series, box, 200)
    expect(nearestIndex(series, scales, scales.x(10) + 3)).toBe(1)
    expect(nearestIndex(series, scales, 0)).toBe(0)
    expect(nearestIndex(series, scales, 9999)).toBe(4)
  })
})
