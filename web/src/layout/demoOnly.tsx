import type { ComponentType } from 'react'
import { PendingFeature } from '@/components/PendingFeature'
import { useSession } from '@/session/useSession'

/**
 * Wraps a screen that only has seeded data so far. A real signed-in user sees a plain "coming" message
 * instead; the demo workspace sees the screen.
 */
export function demoOnly(Page: ComponentType, feature: string): ComponentType {
  return function DemoOnly() {
    const session = useSession()
    if (session.status === 'ready' && session.mode === 'user') return <PendingFeature feature={feature} />
    return <Page />
  }
}
