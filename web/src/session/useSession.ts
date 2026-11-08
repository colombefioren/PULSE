import { useCallback, useMemo, useSyncExternalStore } from 'react'
import { useMe } from '@/api/hooks'
import type { OrgSummary } from '@/api/types'
import { useDemoMode } from './demoMode'

/**
 * Who is using the app and which team they are looking at.
 *
 * - `loading`: the first answer from the API has not arrived.
 * - `anonymous`: nobody is signed in and the demo is not on.
 * - `ready`: either the demo workspace (`mode: 'demo'`) or a real signed-in user (`mode: 'user'`).
 */
export type Session =
  | { status: 'loading' }
  | { status: 'anonymous' }
  | {
      status: 'ready'
      mode: 'demo' | 'user'
      user: { id: string; name: string; email: string; initials: string }
      /** The team being viewed. Null only for a real user who belongs to no team. */
      org: OrgSummary | null
      orgs: OrgSummary[]
      selectOrg: (id: string) => void
    }

const DEMO_ORG: OrgSummary = { id: 'demo', name: 'Pulse Demo', slug: 'pulse-demo', role: 'Owner' }
const ORG_KEY = 'pulse-org'
const listeners = new Set<() => void>()

function readOrgId(): string | null {
  try {
    return localStorage.getItem(ORG_KEY)
  } catch {
    return null
  }
}

function writeOrgId(id: string) {
  try {
    localStorage.setItem(ORG_KEY, id)
  } catch {
    // Not remembering the choice is fine; the first team is used next time.
  }
  listeners.forEach((listener) => listener())
}

function initialsOf(name: string): string {
  return name.trim().charAt(0).toUpperCase() || '?'
}

export function useSession(): Session {
  const demo = useDemoMode()
  const me = useMe()
  const storedOrg = useSyncExternalStore(
    (listener) => {
      listeners.add(listener)
      return () => listeners.delete(listener)
    },
    readOrgId,
    () => null,
  )
  const selectOrg = useCallback((id: string) => writeOrgId(id), [])

  return useMemo<Session>(() => {
    if (demo) {
      return {
        status: 'ready',
        mode: 'demo',
        user: { id: 'demo-user', name: 'Colombe', email: 'demo@pulse.dev', initials: 'C' },
        org: DEMO_ORG,
        orgs: [DEMO_ORG],
        selectOrg,
      }
    }
    if (me.isPending) return { status: 'loading' }
    if (!me.data) return { status: 'anonymous' }

    const orgs = me.data.organizations
    const org = orgs.find((o) => o.id === storedOrg) ?? orgs[0] ?? null
    return {
      status: 'ready',
      mode: 'user',
      user: { id: me.data.id, name: me.data.name, email: me.data.email, initials: initialsOf(me.data.name) },
      org,
      orgs,
      selectOrg,
    }
  }, [demo, me.isPending, me.data, storedOrg, selectOrg])
}

/** True when a signed-in person can change the team's data (Owner or Admin). The demo counts as read-write. */
export function canManage(role: OrgSummary['role'] | undefined): boolean {
  return role === 'Owner' || role === 'Admin'
}
