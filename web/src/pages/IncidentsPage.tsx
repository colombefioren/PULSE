import { TriangleAlert } from 'lucide-react'
import { EmptyState } from '@/components/EmptyState'
import { PageHeader } from '@/components/PageHeader'

export default function IncidentsPage() {
  return (
    <>
      <PageHeader title="Incidents" description="Outages detected by monitors and incidents you post by hand." />
      <EmptyState
        icon={TriangleAlert}
        title="No incidents"
        description="An incident opens on its own after consecutive failed checks, and resolves after consecutive successes."
      />
    </>
  )
}
