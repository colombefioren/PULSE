import { beforeEach, describe, expect, it } from 'vitest'
import { THEME_STORAGE_KEY, nextPreference, readStoredPreference, resolveTheme, storePreference } from './theme'

describe('resolveTheme', () => {
  it('follows the system setting when the preference is system', () => {
    expect(resolveTheme('system', true)).toBe('dark')
    expect(resolveTheme('system', false)).toBe('light')
  })

  it('ignores the system setting once a theme is chosen', () => {
    expect(resolveTheme('light', true)).toBe('light')
    expect(resolveTheme('dark', false)).toBe('dark')
  })
})

describe('nextPreference', () => {
  it('cycles system, light, dark and back to system', () => {
    expect(nextPreference('system')).toBe('light')
    expect(nextPreference('light')).toBe('dark')
    expect(nextPreference('dark')).toBe('system')
  })
})

describe('stored preference', () => {
  beforeEach(() => localStorage.clear())

  it('defaults to system when nothing is stored', () => {
    expect(readStoredPreference()).toBe('system')
  })

  it('round-trips a chosen theme and clears it for system', () => {
    storePreference('dark')
    expect(readStoredPreference()).toBe('dark')

    storePreference('system')
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBeNull()
  })

  it('treats an unknown stored value as system', () => {
    localStorage.setItem(THEME_STORAGE_KEY, 'purple')
    expect(readStoredPreference()).toBe('system')
  })
})
