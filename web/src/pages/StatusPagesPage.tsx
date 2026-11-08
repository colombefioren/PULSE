import { Globe } from 'lucide-react'
import { EmptyState } from '@/components/EmptyState'
import { PageHeader } from '@/components/PageHeader'

export default function StatusPagesPage() {
  return (
    <>
      <PageHeader title="Status pages" description="Public pages that show customers how your services are doing." />
      <EmptyState
        icon={Globe}
        title="No status pages yet"
        description="Group monitors into components and publish them at /status/your-slug with 90 days of uptime history."
      />
    </>
  )
}
