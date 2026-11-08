import { describe, expect, it } from 'vitest'
import { safeNext } from './redirect'

describe('safeNext', () => {
  it('keeps same-site paths, including a query string', () => {
    expect(safeNext('/invites/abc')).toBe('/invites/abc')
    expect(safeNext('/settings?tab=team')).toBe('/settings?tab=team')
  })

  it('falls back when there is nothing to follow', () => {
    expect(safeNext(null)).toBe('/dashboard')
    expect(safeNext('')).toBe('/dashboard')
    expect(safeNext(undefined, '/x')).toBe('/x')
  })

  it.each(['https://evil.example', '//evil.example', '/\\evil.example', 'javascript:alert(1)', 'evil'])(
    'refuses %s',
    (value) => expect(safeNext(value)).toBe('/dashboard'),
  )
})
