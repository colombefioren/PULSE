/**
 * A "go here after signing in" value from the address bar or router state. Only same-site paths pass,
 * so a crafted link cannot send someone to another site after they sign in.
 */
export function safeNext(value: string | null | undefined, fallback = '/dashboard'): string {
  if (!value) return fallback
  if (!value.startsWith('/') || value.startsWith('//') || value.includes('\\')) return fallback
  return value
}
