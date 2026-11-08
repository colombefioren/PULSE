import { describe, expect, it } from 'vitest'
import { contrast, isHexColor, luminance, readableOn } from './color'

describe('accent colour helpers', () => {
  it('measures luminance from black to white', () => {
    expect(luminance('#000000')).toBe(0)
    expect(luminance('#ffffff')).toBeCloseTo(1, 5)
    expect(luminance('#fac303')).toBeGreaterThan(0.5)
    expect(luminance('#nope')).toBeNull()
    expect(luminance('#fff')).toBeNull()
  })

  it('knows black on white is the strongest contrast', () => {
    expect(contrast('#000000', '#ffffff')).toBeCloseTo(21, 0)
    expect(contrast('#ffffff', '#ffffff')).toBe(1)
    expect(contrast('x', '#ffffff')).toBeNull()
  })

  it('picks dark text on light accents and light text on dark ones', () => {
    expect(readableOn('#fac303')).toBe('#111111')
    expect(readableOn('#ffffff')).toBe('#111111')
    expect(readableOn('#111827')).toBe('#ffffff')
    expect(readableOn('#1d4ed8')).toBe('#ffffff')
  })

  it('reaches AA contrast for normal text on common accents', () => {
    for (const accent of ['#fac303', '#3b82f6', '#10b981', '#ef4444', '#f97316', '#0ea5e9', '#64748b', '#000000', '#ffffff']) {
      expect(contrast(accent, readableOn(accent))).toBeGreaterThanOrEqual(4.5)
    }
  })

  it('always picks the better of the two, even for mid tones where neither reaches AA', () => {
    const purple = '#8b5cf6'
    const picked = contrast(purple, readableOn(purple))!
    expect(picked).toBeGreaterThanOrEqual(contrast(purple, '#111111')!)
    expect(picked).toBeGreaterThanOrEqual(contrast(purple, '#ffffff')!)
    expect(picked).toBeLessThan(4.5)
  })

  it('falls back to dark text for something that is not a colour', () => {
    expect(readableOn('yellow')).toBe('#111111')
  })

  it('accepts only six digit hex colours', () => {
    expect(isHexColor('#fac303')).toBe(true)
    expect(isHexColor('#FAC303')).toBe(true)
    expect(isHexColor('#fac')).toBe(false)
    expect(isHexColor('fac303')).toBe(false)
    expect(isHexColor('red')).toBe(false)
  })
})
