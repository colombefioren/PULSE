import { ArrowDown, ArrowLeft, ArrowUp, ExternalLink, ImagePlus, Pencil, Plus, Trash2 } from 'lucide-react'
import { useId, useRef, useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { ApiError } from '@/api/client'
import { ComponentFormDialog } from '@/components/ComponentFormDialog'
import { ConfirmDialog } from '@/components/ConfirmDialog'
import { EmptyState } from '@/components/EmptyState'
import { Notice } from '@/components/Notice'
import { Panel } from '@/components/Panel'
import { TextField } from '@/components/TextField'
import { useIncidentAccess } from '@/data/incidents'
import { useMonitorList } from '@/data/monitors'
import {
  useDeleteComponent,
  useDeleteStatusPage,
  useRemoveLogo,
  useReorderComponents,
  useStatusPageDetail,
  useUpdateStatusPage,
  useUploadLogo,
  type ComponentView,
  type StatusPageView,
} from '@/data/statusPages'
import { contrast, isHexColor, readableOn } from '@/lib/color'
import { slugProblem } from '@/lib/slug'

const LOGO_TYPES = ['image/png', 'image/jpeg', 'image/webp']
const LOGO_MAX_BYTES = 256 * 1024
const DEFAULT_ACCENT = '#fac303'

function Settings({ page, canEdit }: { page: StatusPageView; canEdit: boolean }) {
  const save = useUpdateStatusPage(page.id)
  const checkboxId = useId()
  const accentId = useId()
  const [title, setTitle] = useState(page.title)
  const [slug, setSlug] = useState(page.slug)
  const [accent, setAccent] = useState(page.accent)
  const [footer, setFooter] = useState(page.footer)
  const [showResponseTimes, setShowResponseTimes] = useState(page.showResponseTimes)
  const [errors, setErrors] = useState<{ title?: string; slug?: string; accent?: string; footer?: string }>({})
  const [saved, setSaved] = useState(false)

  const failure = save.error instanceof ApiError ? save.error : null
  const server = (field: string) => failure?.fields[field]?.[0] ?? failure?.fields[field.charAt(0).toUpperCase() + field.slice(1)]?.[0]
  const banner = failure && Object.keys(failure.fields).length === 0 ? failure.message : null

  const submit = (event: FormEvent) => {
    event.preventDefault()
    setSaved(false)
    const found: typeof errors = {}
    if (!title.trim()) found.title = 'Enter a title'
    const slugIssue = slugProblem(slug)
    if (slugIssue) found.slug = slugIssue
    if (accent && !isHexColor(accent)) found.accent = 'Use a colour like #fac303'
    if (footer.length > 300) found.footer = 'Use 300 characters or fewer'
    setErrors(found)
    if (Object.keys(found).length > 0) return

    save.mutate({ title, slug, accent, footer, showResponseTimes }, { onSuccess: () => setSaved(true) })
  }

  const swatch = isHexColor(accent) ? accent : DEFAULT_ACCENT
  const readable = (contrast(swatch, readableOn(swatch)) ?? 21) >= 4.5

  return (
    <Panel title="Settings">
      <form onSubmit={submit} noValidate className="flex flex-col gap-5">
        {banner && <Notice tone="error">{banner}</Notice>}
        {saved && <Notice tone="success">Saved. The public page is up to date.</Notice>}

        <TextField label="Title" value={title} disabled={!canEdit} error={errors.title ?? server('title')} onChange={(e) => setTitle(e.target.value)} />

        <TextField
          label="Address"
          autoCapitalize="none"
          spellCheck={false}
          disabled={!canEdit}
          hint={<>Visitors open <span className="mono">/status/{slug || 'your-address'}</span>. Changing it breaks links you already shared.</>}
          value={slug}
          error={errors.slug ?? server('slug')}
          onChange={(e) => setSlug(e.target.value.toLowerCase())}
        />

        <div className="field">
          <label htmlFor={accentId} className="field-label">Accent colour</label>
          <div className="flex items-center gap-3">
            <input
              type="color"
              aria-label="Pick an accent colour"
              className="size-10 shrink-0 cursor-pointer rounded-control border border-border-strong bg-transparent p-1"
              value={swatch}
              disabled={!canEdit}
              onChange={(e) => setAccent(e.target.value)}
            />
            <input
              id={accentId}
              className="input mono max-w-40"
              value={accent}
              placeholder={DEFAULT_ACCENT}
              spellCheck={false}
              disabled={!canEdit}
              aria-invalid={errors.accent || server('accentColor') ? true : undefined}
              onChange={(e) => setAccent(e.target.value.trim())}
            />
          </div>
          {(errors.accent ?? server('accentColor')) ? (
            <p className="field-error" role="alert">{errors.accent ?? server('accentColor')}</p>
          ) : (
            <p className="field-hint">Used for the stripe and the subscribe button. Leave blank for the Pulse yellow.</p>
          )}
          {!readable && !(errors.accent ?? server('accentColor')) && (
            <p className="field-hint" role="status">Text on a button this colour is hard to read. A lighter or darker shade works better.</p>
          )}
        </div>

        <div className="field">
          <label htmlFor={`${checkboxId}-footer`} className="field-label">Footer</label>
          <textarea
            id={`${checkboxId}-footer`}
            className="input min-h-20 py-3"
            disabled={!canEdit}
            value={footer}
            aria-invalid={errors.footer || server('footer') ? true : undefined}
            onChange={(e) => setFooter(e.target.value)}
          />
          {(errors.footer ?? server('footer')) ? (
            <p className="field-error" role="alert">{errors.footer ?? server('footer')}</p>
          ) : (
            <p className="field-hint">A line of contact details or a link to your support page. {footer.length}/300</p>
          )}
        </div>

        <label className="flex min-h-9 cursor-pointer items-center gap-3 text-base">
          <input
            type="checkbox"
            className="size-4 accent-[var(--brand-ink)]"
            checked={showResponseTimes}
            disabled={!canEdit}
            onChange={(e) => setShowResponseTimes(e.target.checked)}
          />
          Show average response times
        </label>

        {canEdit && (
          <div className="flex justify-end">
            <button type="submit" className="btn btn-primary" disabled={save.isPending}>
              {save.isPending ? 'Saving…' : 'Save settings'}
            </button>
          </div>
        )}
      </form>
    </Panel>
  )
}

function Logo({ page, canEdit }: { page: StatusPageView; canEdit: boolean }) {
  const upload = useUploadLogo(page.id)
  const remove = useRemoveLogo(page.id)
  const input = useRef<HTMLInputElement>(null)
  const [problem, setProblem] = useState<string | null>(null)

  const failure = upload.error instanceof ApiError ? upload.error.message : remove.error instanceof ApiError ? remove.error.message : null

  const choose = (file: File | undefined) => {
    setProblem(null)
    if (!file) return
    if (!LOGO_TYPES.includes(file.type)) {
      setProblem('Use a PNG, JPEG or WebP image.')
    } else if (file.size > LOGO_MAX_BYTES) {
      setProblem(`Use an image of ${LOGO_MAX_BYTES / 1024} KB or less. This one is ${Math.ceil(file.size / 1024)} KB.`)
    } else {
      upload.mutate(file)
    }
    if (input.current) input.current.value = ''
  }

  return (
    <Panel title="Logo" index={1}>
      <div className="flex flex-wrap items-center gap-4">
        <div className="flex h-16 w-40 items-center justify-center rounded-control border border-border bg-surface-2">
          {page.logoUrl ? (
            <img src={page.logoUrl} alt={`Logo of ${page.title}`} className="max-h-14 max-w-36 object-contain" />
          ) : (
            <span className="text-sm text-muted">No logo</span>
          )}
        </div>
        {canEdit && (
          <div className="flex flex-wrap gap-2">
            <input
              ref={input}
              type="file"
              accept={LOGO_TYPES.join(',')}
              className="sr-only"
              aria-label="Choose a logo file"
              tabIndex={-1}
              onChange={(e) => choose(e.target.files?.[0])}
            />
            <button type="button" className="btn" disabled={upload.isPending} onClick={() => input.current?.click()}>
              <ImagePlus size={16} strokeWidth={1.5} aria-hidden /> {upload.isPending ? 'Uploading…' : page.logoUrl ? 'Replace logo' : 'Upload logo'}
            </button>
            {page.logoUrl && (
              <button type="button" className="btn btn-ghost" disabled={remove.isPending} onClick={() => remove.mutate()}>
                Remove logo
              </button>
            )}
          </div>
        )}
      </div>
      <p className="field-hint mt-3">PNG, JPEG or WebP, up to {LOGO_MAX_BYTES / 1024} KB. Shown next to the title.</p>
      {(problem ?? failure) && <Notice tone="error" className="mt-3">{problem ?? failure}</Notice>}
    </Panel>
  )
}

function Components({ page, canEdit }: { page: StatusPageView; canEdit: boolean }) {
  const monitors = useMonitorList()
  const reorder = useReorderComponents(page.id)
  const remove = useDeleteComponent(page.id)
  const [adding, setAdding] = useState(false)
  const [editing, setEditing] = useState<ComponentView | null>(null)
  const [deleting, setDeleting] = useState<ComponentView | null>(null)

  const names = new Map((monitors.data ?? []).map((m) => [m.id, m.name]))
  const groups = [...new Set(page.components.map((c) => c.group).filter(Boolean))]

  const move = (index: number, by: -1 | 1) => {
    const ids = page.components.map((c) => c.id)
    const target = index + by
    if (target < 0 || target >= ids.length) return
    ;[ids[index], ids[target]] = [ids[target]!, ids[index]!]
    reorder.mutate(ids)
  }

  return (
    <Panel
      title="Components"
      index={2}
      action={
        canEdit && (
          <button type="button" className="btn" onClick={() => setAdding(true)}>
            <Plus size={16} strokeWidth={1.5} aria-hidden /> Add component
          </button>
        )
      }
    >
      {page.components.length === 0 ? (
        <p className="text-base text-muted">
          Nothing is listed yet. A component is one line on the page, like “Website” or “API”, fed by one or more monitors.
        </p>
      ) : (
        <ul className="flex flex-col">
          {page.components.map((component, index) => (
            <li key={component.id} className="flex flex-wrap items-center justify-between gap-3 border-t border-border py-3 first:border-t-0 first:pt-0">
              <div className="min-w-0">
                <p className="font-medium">
                  {component.name}
                  {component.group && <span className="ml-2 text-sm font-normal text-subtle">{component.group}</span>}
                </p>
                <p className="mt-0.5 truncate text-sm text-muted">
                  {component.monitorIds.map((id) => names.get(id) ?? 'Monitor').join(', ')}
                </p>
              </div>
              {canEdit && (
                <div className="flex gap-1">
                  <button type="button" className="btn btn-ghost btn-icon" aria-label={`Move ${component.name} up`} disabled={index === 0 || reorder.isPending} onClick={() => move(index, -1)}>
                    <ArrowUp size={16} strokeWidth={1.5} aria-hidden />
                  </button>
                  <button type="button" className="btn btn-ghost btn-icon" aria-label={`Move ${component.name} down`} disabled={index === page.components.length - 1 || reorder.isPending} onClick={() => move(index, 1)}>
                    <ArrowDown size={16} strokeWidth={1.5} aria-hidden />
                  </button>
                  <button type="button" className="btn btn-ghost btn-icon" aria-label={`Edit ${component.name}`} onClick={() => setEditing(component)}>
                    <Pencil size={16} strokeWidth={1.5} aria-hidden />
                  </button>
                  <button type="button" className="btn btn-ghost btn-icon" aria-label={`Delete ${component.name}`} onClick={() => setDeleting(component)}>
                    <Trash2 size={16} strokeWidth={1.5} aria-hidden />
                  </button>
                </div>
              )}
            </li>
          ))}
        </ul>
      )}

      {reorder.error instanceof ApiError && <Notice tone="error" className="mt-3">{reorder.error.message}</Notice>}

      <ComponentFormDialog open={adding} onOpenChange={setAdding} pageId={page.id} groups={groups} />
      {editing && (
        <ComponentFormDialog open onOpenChange={(open) => !open && setEditing(null)} pageId={page.id} component={editing} groups={groups} />
      )}
      <ConfirmDialog
        open={deleting !== null}
        onOpenChange={(open) => !open && setDeleting(null)}
        title={`Remove ${deleting?.name ?? 'component'}?`}
        description="It disappears from the public page. Your monitors are not touched."
        confirmLabel="Remove component"
        pending={remove.isPending}
        error={remove.error instanceof ApiError ? remove.error.message : null}
        onConfirm={() => deleting && remove.mutate(deleting.id, { onSuccess: () => setDeleting(null) })}
      />
    </Panel>
  )
}

function Danger({ page }: { page: StatusPageView }) {
  const navigate = useNavigate()
  const remove = useDeleteStatusPage()
  const [open, setOpen] = useState(false)

  return (
    <Panel title="Delete this page" index={4}>
      <p className="text-base text-muted">The address stops working and the {page.subscriberCount} confirmed subscribers are removed. Your monitors and incidents are kept.</p>
      <button type="button" className="btn mt-4" onClick={() => setOpen(true)}>
        <Trash2 size={16} strokeWidth={1.5} aria-hidden /> Delete status page
      </button>
      <ConfirmDialog
        open={open}
        onOpenChange={setOpen}
        title={`Delete ${page.title}?`}
        description="Anyone who bookmarked it will see a “no status page” message. This cannot be undone."
        confirmLabel="Delete status page"
        pending={remove.isPending}
        error={remove.error instanceof ApiError ? remove.error.message : null}
        onConfirm={() => remove.mutate(page.id, { onSuccess: () => void navigate('/status-pages') })}
      />
    </Panel>
  )
}

export default function StatusPageEditorPage() {
  const { id = '' } = useParams()
  const { canEdit } = useIncidentAccess()
  const page = useStatusPageDetail(id)

  if (page.isPending) return <div className="skeleton h-64" aria-label="Loading status page" />
  if (page.isError) {
    return (
      <Notice tone="error">
        Could not load this status page.{' '}
        <button type="button" className="underline" onClick={() => void page.refetch()}>Try again</button>
      </Notice>
    )
  }
  if (!page.data) {
    return (
      <EmptyState
        icon={ArrowLeft}
        title="Status page not found"
        description="It may have been deleted, or it belongs to another team."
        action={<Link to="/status-pages" className="btn">All status pages</Link>}
      />
    )
  }

  const data = page.data

  return (
    <div className="flex flex-col gap-6">
      <Link to="/status-pages" className="inline-flex w-fit items-center gap-1.5 text-sm text-muted hover:text-fg">
        <ArrowLeft size={16} strokeWidth={1.5} aria-hidden /> All status pages
      </Link>

      <header className="flex flex-wrap items-start justify-between gap-4">
        <div className="min-w-0">
          <h1 className="text-xl">{data.title}</h1>
          <p className="mono mt-1 break-all text-xs text-muted">/status/{data.slug}</p>
        </div>
        <Link to={`/status/${data.slug}`} className="btn">
          View public page <ExternalLink size={16} strokeWidth={1.5} aria-hidden />
        </Link>
      </header>

      {!canEdit && <Notice tone="success">You can look at this page, but only Owners and Admins can change it.</Notice>}

      <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_minmax(0,1fr)]">
        <div className="flex flex-col gap-4">
          <Settings page={data} canEdit={canEdit} />
        </div>
        <div className="flex flex-col gap-4">
          <Logo page={data} canEdit={canEdit} />
          <Components page={data} canEdit={canEdit} />
          <Panel title="Subscribers" index={3}>
            <p className="text-base">
              <strong className="text-lg">{data.subscriberCount}</strong> confirmed {data.subscriberCount === 1 ? 'subscriber' : 'subscribers'}
            </p>
            <p className="field-hint mt-1">They get an email when an incident on this page opens, changes or resolves. Visitors confirm by email before they count.</p>
          </Panel>
          {canEdit && <Danger page={data} />}
        </div>
      </div>
    </div>
  )
}
