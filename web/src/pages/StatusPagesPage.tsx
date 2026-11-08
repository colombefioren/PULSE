import { ExternalLink, Globe, Pencil, Plus } from 'lucide-react'
import { useState } from 'react'
import { Link, useNavigate } from 'react-router'
import { EmptyState } from '@/components/EmptyState'
import { Notice } from '@/components/Notice'
import { PageHeader } from '@/components/PageHeader'
import { StatusPageFormDialog } from '@/components/StatusPageFormDialog'
import { StatusPill } from '@/components/StatusPill'
import { useIncidentAccess } from '@/data/incidents'
import { useStatusPage } from '@/data/queries'
import { useStatusPages, type StatusPageView } from '@/data/statusPages'
import { groupComponents, overallStatus } from '@/lib/overall'

/** The demo workspace has one simulated page; this shows it the way a team's own page would be listed. */
function DemoPage() {
  const { data, isPending } = useStatusPage('pulse-demo')

  if (isPending) return <div className="skeleton h-40" aria-label="Loading status pages" />
  if (!data) return null

  return (
    <article className="card rise p-5">
      <header className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="text-md">{data.title}</h2>
          <p className="mono mt-1 text-xs text-muted">/status/{data.slug}</p>
        </div>
        <Link to={`/status/${data.slug}`} className="btn">
          View public page <ExternalLink size={16} strokeWidth={1.5} aria-hidden />
        </Link>
      </header>
      <p className="mt-4 text-base font-medium">{overallStatus(data.components).headline}</p>
      <ul className="mt-3 flex flex-col">
        {groupComponents(data.components).flatMap(([group, components]) =>
          components.map((component) => (
            <li key={component.id} className="flex items-center justify-between gap-3 border-t border-border py-2.5 text-sm">
              <span>
                {component.name} <span className="text-subtle">· {group}</span>
              </span>
              <StatusPill status={component.status} />
            </li>
          )),
        )}
      </ul>
    </article>
  )
}

function PageCard({ page, index }: { page: StatusPageView; index: number }) {
  const count = page.components.length

  return (
    <li className="card rise p-5" style={{ '--i': Math.min(index, 8) } as React.CSSProperties}>
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <h3 className="text-md">{page.title}</h3>
          <p className="mono mt-1 break-all text-xs text-muted">/status/{page.slug}</p>
          <p className="mt-2 text-sm text-muted">
            {count === 0 ? 'No components yet' : `${count} ${count === 1 ? 'component' : 'components'}`} ·{' '}
            {page.subscriberCount} {page.subscriberCount === 1 ? 'subscriber' : 'subscribers'}
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          <Link to={`/status/${page.slug}`} className="btn">
            View public page <ExternalLink size={16} strokeWidth={1.5} aria-hidden />
          </Link>
          <Link to={`/status-pages/${page.id}`} className="btn" aria-label={`Edit ${page.title}`}>
            <Pencil size={16} strokeWidth={1.5} aria-hidden /> Edit
          </Link>
        </div>
      </div>
    </li>
  )
}

export default function StatusPagesPage() {
  const navigate = useNavigate()
  const { demoMode, canEdit } = useIncidentAccess()
  const pages = useStatusPages()
  const [creating, setCreating] = useState(false)

  return (
    <>
      <PageHeader
        title="Status pages"
        description="Public pages that show customers how your services are doing."
        actions={
          canEdit && (
            <button type="button" className="btn btn-primary" onClick={() => setCreating(true)}>
              <Plus size={16} strokeWidth={1.5} aria-hidden /> Create status page
            </button>
          )
        }
      />

      {demoMode ? (
        <>
          <Notice tone="success" className="mb-4">This is the demo status page, drawn from simulated monitors. Sign up to publish your own.</Notice>
          <DemoPage />
        </>
      ) : pages.isPending ? (
        <div className="skeleton h-32" aria-label="Loading status pages" />
      ) : pages.isError ? (
        <Notice tone="error">
          Could not load status pages.{' '}
          <button type="button" className="underline" onClick={() => void pages.refetch()}>Try again</button>
        </Notice>
      ) : pages.data.length === 0 ? (
        <EmptyState
          icon={Globe}
          title="No status pages yet"
          description="Group monitors into components and publish them at /status/your-address with 90 days of uptime history."
          action={canEdit ? <button type="button" className="btn btn-primary" onClick={() => setCreating(true)}>Create status page</button> : undefined}
        />
      ) : (
        <ul className="flex flex-col gap-4">
          {pages.data.map((page, i) => <PageCard key={page.id} page={page} index={i} />)}
        </ul>
      )}

      <StatusPageFormDialog open={creating} onOpenChange={setCreating} onCreated={(id) => void navigate(`/status-pages/${id}`)} />
    </>
  )
}
