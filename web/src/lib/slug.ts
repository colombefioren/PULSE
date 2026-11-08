/** A suggested address for a status page: lower case words joined by hyphens, at most 40 characters. */
export function slugify(text: string): string {
  return text
    .normalize('NFKD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 40)
    .replace(/-+$/g, '')
}

const RESERVED = new Set([
  'api', 'admin', 'app', 'login', 'signup', 'status', 'demo', 'dashboard', 'monitors', 'incidents', 'alerts',
  'settings', 'static', 'assets', 'health', 'badge', 'public', 'www', 'pulse',
])

/** The rules the server enforces, so a mistake is caught before the request. Returns a message, or null when fine. */
export function slugProblem(slug: string): string | null {
  if (!slug) return 'Choose an address for the page'
  if (slug.length < 3) return 'Use at least 3 characters'
  if (slug.length > 40) return 'Use 40 characters or fewer'
  if (!/^[a-z0-9](?:[a-z0-9-]*[a-z0-9])$/.test(slug)) return 'Use lower case letters, numbers and hyphens, starting and ending with a letter or number'
  if (RESERVED.has(slug)) return 'That word is used by the app. Pick another'
  return null
}
