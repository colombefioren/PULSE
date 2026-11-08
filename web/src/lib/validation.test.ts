import { describe, expect, it } from 'vitest'
import { passwordStrength, slugify, validateEmail, validateName, validatePassword } from './validation'

describe('validateEmail', () => {
  it('accepts ordinary addresses', () => {
    expect(validateEmail('colombe@pulse.dev')).toBeNull()
    expect(validateEmail('  a.b+tag@sub.example.co  ')).toBeNull()
  })

  it('says what is wrong', () => {
    expect(validateEmail('')).toBe('Enter your email address')
    expect(validateEmail('colombe')).toBe('Enter an email like name@company.com')
    expect(validateEmail('a@b')).toBe('Enter an email like name@company.com')
  })
})

describe('validateName', () => {
  it('names the field in the message', () => {
    expect(validateName('', 'your name')).toBe('Enter your name')
    expect(validateName('A', 'your name')).toBe('Your name needs at least 2 characters')
    expect(validateName('Ana', 'your name')).toBeNull()
  })
})

describe('validatePassword', () => {
  it('requires ten characters and says how many are missing', () => {
    expect(validatePassword('')).toBe('Choose a password')
    expect(validatePassword('short')).toBe('Use at least 10 characters (5 more)')
    expect(validatePassword('long enough!')).toBeNull()
  })
})

describe('passwordStrength', () => {
  it('rewards length and variety', () => {
    expect(passwordStrength('')).toBe(0)
    expect(passwordStrength('abcdefghij')).toBe(1)
    expect(passwordStrength('abcdefghijklmn')).toBe(2)
    expect(passwordStrength('Abcdefghij1k')).toBe(2)
    expect(passwordStrength('Abcdefghij1k!')).toBeGreaterThanOrEqual(3)
    expect(passwordStrength('Correct-Horse-Battery-9')).toBe(4)
  })

  it('does not call a short password better than weak however varied it is', () => {
    expect(passwordStrength('Aa1!')).toBe(1)
  })
})

describe('slugify', () => {
  it('makes a URL-safe slug', () => {
    expect(slugify('Acme Platform!')).toBe('acme-platform')
    expect(slugify('  Café  Société ')).toBe('cafe-societe')
    expect(slugify('---')).toBe('')
  })

  it('caps the length', () => {
    expect(slugify('a'.repeat(100))).toHaveLength(40)
  })
})
