import { Globe } from 'lucide-react'
import { useEffect } from 'react'
import { Link, useParams, useSearchParams } from 'react-router'
import { EmptyState } from '@/components/EmptyState'
import { Notice } from '@/components/Notice'
import { StatusPageView } from '@/components/StatusPageView'
import { usePublicStatus, useStatusLive } from '@/data/statusPages'

const NOTICE: Record<string, string> = {
  confirmed: 'You are subscribed. We will email you when something changes.',
  removed: 'You are unsubscribed. No more emails from this page.',
}

/** /status/:slug. No sign-in, no app chrome. */
export default function PublicStatusPage() {
  const { slug = '' } = useParams()
  const { data, isPending, isError, refetch } = usePublicStatus(slug)
  const [params] = useSearchParams()
  useStatusLive(slug)

  useEffect(() => {
    if (data) document.title = data.title
  }, [data])

  const notice = NOTICE[params.get('subscription') ?? '']

  return (
    <main className="min-h-dvh bg-bg">
      {isPending ? (
        <div className="mx-auto flex max-w-3xl flex-col gap-4 px-4 py-14" aria-label="Loading status page">
          <div className="skeleton h-10 w-64" aria-hidden />
          <div className="skeleton h-24" aria-hidden />
          <div className="skeleton h-40" aria-hidden />
        </div>
      ) : isError ? (
        <div className="mx-auto max-w-3xl px-4 py-14">
          <Notice tone="error">
            Could not load this status page.{' '}
            <button type="button" className="underline" onClick={() => void refetch()}>Try again</button>
          </Notice>
        </div>
      ) : data ? (
        <>
          {notice && (
            <div className="mx-auto max-w-3xl px-4 pt-6">
              <Notice tone="success">{notice}</Notice>
            </div>
          )}
          <StatusPageView page={data} />
        </>
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
    </main>
  )
}
