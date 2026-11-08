import { Activity, Plus } from 'lucide-react'
import { EmptyState } from '@/components/EmptyState'
import { PageHeader } from '@/components/PageHeader'

export default function MonitorsPage() {
  return (
    <>
      <PageHeader title="Monitors" description="Websites and APIs checked on a schedule." />
      <EmptyState
        icon={Activity}
        title="No monitors yet"
        description="Add a URL and Pulse checks it every 30 seconds to 30 minutes, then alerts your team when it goes down."
        action={
          <button type="button" className="btn btn-primary" disabled title="Monitors arrive with the check engine">
            <Plus size={16} strokeWidth={1.5} aria-hidden />
            Add your first monitor
          </button>
        }
      />
    </>
  )
}
