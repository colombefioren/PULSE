const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/

export function validateEmail(value: string): string | null {
  const email = value.trim()
  if (!email) return 'Enter your email address'
  if (!EMAIL.test(email)) return 'Enter an email like name@company.com'
  return null
}

export function validateName(value: string, what: string): string | null {
  const name = value.trim()
  if (!name) return `Enter ${what}`
  if (name.length < 2) return `${what[0]?.toUpperCase()}${what.slice(1)} needs at least 2 characters`
  return null
}

export function validatePassword(value: string): string | null {
  if (!value) return 'Choose a password'
  if (value.length < 10) return `Use at least 10 characters (${10 - value.length} more)`
  return null
}

export type Strength = 0 | 1 | 2 | 3 | 4

/** 0 to 4. Length counts most; mixing character classes adds the rest. Not a substitute for a breach check. */
export function passwordStrength(value: string): Strength {
  if (value.length === 0) return 0
  // Below the minimum length nothing makes a password better than weak.
  if (value.length < 10) return 1
  let score = 0
  if (value.length >= 10) score += 1
  if (value.length >= 14) score += 1
  const classes = [/[a-z]/, /[A-Z]/, /\d/, /[^A-Za-z0-9]/].filter((re) => re.test(value)).length
  if (classes >= 3) score += 1
  if (classes === 4 && value.length >= 12) score += 1
  return Math.min(4, score) as Strength
}

export const STRENGTH_LABEL: Record<Strength, string> = {
  0: '',
  1: 'Weak',
  2: 'Fair',
  3: 'Good',
  4: 'Strong',
}

/** Turns an organization name into a URL slug: "Acme Platform!" -> "acme-platform". */
export function slugify(value: string): string {
  return value
    .normalize('NFKD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 40)
}
