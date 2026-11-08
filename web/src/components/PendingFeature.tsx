import { ArrowRight, Hourglass } from 'lucide-react'
import { Link } from 'react-router'
import { EmptyState } from './EmptyState'
import { PageHeader } from './PageHeader'

/**
 * Shown to real accounts on screens whose backend is not built yet. It says so plainly instead of showing
 * seeded numbers that would look like the team's own data.
 */
export function PendingFeature({ feature }: { feature: string }) {
  return (
    <>
      <PageHeader title={feature} />
      <EmptyState
        icon={Hourglass}
        title={`${feature} arrive in an upcoming release`}
        description="Monitors, incidents and alerts are live for your team. This screen is next. The demo workspace shows how it will look with real data."
        action={
          <Link to="/demo" className="btn btn-primary">
            Open the demo <ArrowRight size={16} strokeWidth={1.5} aria-hidden />
          </Link>
        }
      />
    </>
  )
}
