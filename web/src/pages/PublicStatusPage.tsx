import { Globe } from 'lucide-react'
import { Link, useParams } from 'react-router'
import { EmptyState } from '@/components/EmptyState'
import { StatusPageView } from '@/components/StatusPageView'
import { useStatusPage } from '@/data/queries'

/** /status/:slug. No sign-in, no app chrome. */
export default function PublicStatusPage() {
  const { slug = '' } = useParams()
  const { data, isPending } = useStatusPage(slug)

  return (
    <div className="min-h-dvh bg-bg">
      {isPending ? (
        <div className="mx-auto flex max-w-3xl flex-col gap-4 px-4 py-14" aria-label="Loading status page">
          <div className="skeleton h-10 w-64" aria-hidden />
          <div className="skeleton h-24" aria-hidden />
          <div className="skeleton h-40" aria-hidden />
        </div>
      ) : data ? (
        <StatusPageView page={data} />
      ) : (
        <div className="mx-auto max-w-3xl px-4 py-14">
          <EmptyState
            icon={Globe}
            title="No status page here"
            description={`There is no status page at /status/${slug}. Check the address with whoever sent it to you.`}
            action={<Link to="/" className="btn">Go to Pulse</Link>}
          />
        </div>
      )}
    </div>
  )
}
