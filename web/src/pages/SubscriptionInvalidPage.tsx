import { MailX } from 'lucide-react'
import { Link } from 'react-router'
import { EmptyState } from '@/components/EmptyState'

/** Where an old or mistyped confirmation or unsubscribe link ends up. */
export default function SubscriptionInvalidPage() {
  return (
    <div className="mx-auto min-h-dvh max-w-3xl px-4 py-14">
      <EmptyState
        icon={MailX}
        title="That link no longer works"
        description="A confirmation link works once, and an unsubscribe link ends the subscription the first time it is used. If you still want updates, open the status page and subscribe again."
        action={<Link to="/" className="btn">Go to Pulse</Link>}
      />
    </div>
  )
}
