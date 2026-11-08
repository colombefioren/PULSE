import { describe, expect, it } from 'vitest'
import { dayState, formatDuration, percentile, relativeTime, summarize, uptimePercent } from './stats'

describe('uptimePercent', () => {
  it('rounds to one decimal', () => {
    expect(uptimePercent(2879, 2880)).toBe(100)
    expect(uptimePercent(2870, 2880)).toBe(99.7)
    expect(uptimePercent(1, 3)).toBe(33.3)
  })

  it('treats no checks as fully up instead of dividing by zero', () => {
    expect(uptimePercent(0, 0)).toBe(100)
  })
})

describe('percentile', () => {
  const values = [10, 20, 30, 40, 50, 60, 70, 80, 90, 100]

  it('uses nearest rank', () => {
    expect(percentile(values, 50)).toBe(50)
    expect(percentile(values, 95)).toBe(100)
    expect(percentile(values, 10)).toBe(10)
  })

  it('does not depend on input order and does not mutate it', () => {
    const shuffled = [90, 10, 50, 30, 70]
    expect(percentile(shuffled, 50)).toBe(50)
    expect(shuffled).toEqual([90, 10, 50, 30, 70])
  })

  it('returns 0 for no values', () => {
    expect(percentile([], 95)).toBe(0)
  })
})

describe('summarize', () => {
  it('counts failed checks against uptime and ignores them for latency', () => {
    const summary = summarize([
      { t: 1, ms: 100 },
      { t: 2, ms: 200 },
      { t: 3, ms: null },
      { t: 4, ms: 300 },
    ])

    expect(summary).toMatchObject({ uptimePct: 75, avgMs: 200, p50: 200, failures: 1, checks: 4 })
  })

  it('handles a series where every check failed', () => {
    const summary = summarize([
      { t: 1, ms: null },
      { t: 2, ms: null },
    ])

    expect(summary).toMatchObject({ uptimePct: 0, avgMs: 0, p95: 0, failures: 2 })
  })
})

describe('dayState', () => {
  it('buckets a day by its uptime', () => {
    expect(dayState(100)).toBe('up')
    expect(dayState(99.9)).toBe('up')
    expect(dayState(99.5)).toBe('degraded')
    expect(dayState(94.9)).toBe('down')
    expect(dayState(null)).toBe('none')
  })
})

describe('relativeTime', () => {
  const now = 1_000_000_000

  it('picks the largest fitting unit', () => {
    expect(relativeTime(now - 12_000, now)).toBe('12s ago')
    expect(relativeTime(now - 4 * 60_000, now)).toBe('4m ago')
    expect(relativeTime(now - 3 * 3_600_000, now)).toBe('3h ago')
    expect(relativeTime(now - 2 * 86_400_000, now)).toBe('2d ago')
  })

  it('reads times that are not in the past as just now', () => {
    expect(relativeTime(now, now)).toBe('just now')
    expect(relativeTime(now + 5000, now)).toBe('just now')
  })
})

describe('formatDuration', () => {
  it('shows the two largest units', () => {
    expect(formatDuration(45)).toBe('45s')
    expect(formatDuration(125)).toBe('2m 5s')
    expect(formatDuration(3600)).toBe('1h')
    expect(formatDuration(2 * 3600 + 14 * 60)).toBe('2h 14m')
    expect(formatDuration(3 * 86400 + 4 * 3600)).toBe('3d 4h')
  })

  it('never goes negative', () => {
    expect(formatDuration(-5)).toBe('0s')
  })
})
