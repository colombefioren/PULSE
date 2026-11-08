import { Bell } from 'lucide-react'
import { EmptyState } from '@/components/EmptyState'
import { PageHeader } from '@/components/PageHeader'

export default function AlertsPage() {
  return (
    <>
      <PageHeader title="Alerts" description="Where Pulse sends a message when a monitor goes down or recovers." />
      <EmptyState
        icon={Bell}
        title="No alert channels yet"
        description="Connect email, Discord, Slack or a signed webhook, then choose which monitors use each channel."
      />
    </>
  )
}
