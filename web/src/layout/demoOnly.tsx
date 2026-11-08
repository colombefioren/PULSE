import type { ComponentType } from 'react'
import { Navigate } from 'react-router'
import { PendingFeature } from '@/components/PendingFeature'
import { useSession } from '@/session/useSession'

/**
 * Wraps a screen that only has seeded data so far. A real signed-in user sees a plain "coming" message
 * (or is sent to `redirectTo` when another screen can stand in); the demo workspace sees the screen.
 */
export function demoOnly(Page: ComponentType, feature: string, redirectTo?: string): ComponentType {
  return function DemoOnly() {
    const session = useSession()
    if (session.status === 'ready' && session.mode === 'user') {
      return redirectTo ? <Navigate to={redirectTo} replace /> : <PendingFeature feature={feature} />
    }
    return <Page />
  }
}
