import { describe, expect, it } from 'vitest'
import { slugify, slugProblem } from './slug'

describe('slugify', () => {
  it('makes lower case words joined by hyphens', () => {
    expect(slugify('Acme Platform Status')).toBe('acme-platform-status')
    expect(slugify('  Hello,   World!  ')).toBe('hello-world')
  })

  it('drops accents and symbols', () => {
    expect(slugify('Café Étoile')).toBe('cafe-etoile')
    expect(slugify('A & B / C')).toBe('a-b-c')
  })

  it('keeps it within 40 characters without a trailing hyphen', () => {
    const slug = slugify('word '.repeat(20))
    expect(slug.length).toBeLessThanOrEqual(40)
    expect(slug.endsWith('-')).toBe(false)
  })

  it('gives an empty string when nothing usable is left', () => {
    expect(slugify('***')).toBe('')
  })
})

describe('slugProblem', () => {
  it('accepts a good address', () => {
    expect(slugProblem('acme-status')).toBeNull()
    expect(slugProblem('a1b')).toBeNull()
  })

  it('explains what is wrong', () => {
    expect(slugProblem('')).toMatch(/Choose/)
    expect(slugProblem('ab')).toMatch(/at least 3/)
    expect(slugProblem('x'.repeat(41))).toMatch(/40/)
    expect(slugProblem('Has Capitals')).toMatch(/lower case/)
    expect(slugProblem('-leading')).toMatch(/lower case/)
    expect(slugProblem('trailing-')).toMatch(/lower case/)
    expect(slugProblem('admin')).toMatch(/used by the app/)
  })
})
