import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router'
import { ApiError } from '@/api/client'
import {
  useChangeRole,
  useCreateInvite,
  useDeleteOrg,
  useInvites,
  useMembers,
  useRemoveMember,
  useRenameOrg,
  useRevokeInvite,
} from '@/api/hooks'
import type { Member, OrgSummary, Role } from '@/api/types'
import { validateEmail, validateName } from '@/lib/validation'
import { canManage } from '@/session/useSession'
import { ConfirmDialog } from './ConfirmDialog'
import { Notice } from './Notice'
import { TextField } from './TextField'

const ROLES: Role[] = ['Owner', 'Admin', 'Viewer']

const ROLE_HELP: Record<Role, string> = {
  Owner: 'Full control, including who else owns the team and deleting it.',
  Admin: 'Manages monitors, status pages and members.',
  Viewer: 'Can look, cannot change anything.',
}

function messageOf(error: unknown): string | null {
  if (!error) return null
  return error instanceof ApiError ? error.message : 'Something went wrong. Try again.'
}

function dateOf(iso: string): string {
  return new Date(iso).toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' })
}

interface TeamSettingsProps {
  org: OrgSummary
  userId: string
}

/** Rename, members, invitations and the danger zone for the team being viewed. */
export function TeamSettings({ org, userId }: TeamSettingsProps) {
  const manage = canManage(org.role)
  const isOwner = org.role === 'Owner'

  return (
    <>
      <RenameSection org={org} editable={manage} />
      <MembersSection org={org} userId={userId} manage={manage} isOwner={isOwner} />
      {manage && <InvitesSection org={org} isOwner={isOwner} />}
      <DangerSection org={org} userId={userId} isOwner={isOwner} />
    </>
  )
}

function RenameSection({ org, editable }: { org: OrgSummary; editable: boolean }) {
  const rename = useRenameOrg(org.id)
  const [name, setName] = useState(org.name)
  const [touched, setTouched] = useState(false)
  const error = touched ? validateName(name, 'a team name') : null
  const unchanged = name.trim() === org.name

  const submit = (event: FormEvent) => {
    event.preventDefault()
    setTouched(true)
    if (validateName(name, 'a team name') !== null || unchanged) return
    rename.mutate(name.trim(), { onSuccess: () => setTouched(false) })
  }

  return (
    <section aria-labelledby="team-name" className="card max-w-xl p-6">
      <h2 id="team-name" className="text-md text-fg">
        Team
      </h2>
      <p className="mt-1 text-base text-muted">
        You are <span className="font-semibold text-fg">{org.role}</span> of this team.
      </p>
      <form onSubmit={submit} noValidate className="mt-4 flex flex-col gap-4">
        {rename.error && <Notice tone="error">{messageOf(rename.error)}</Notice>}
        {rename.isSuccess && !touched && <Notice tone="success">Team renamed.</Notice>}
        <TextField
          label="Team name"
          value={name}
          disabled={!editable}
          error={error}
          hint={editable ? undefined : 'Only Owners and Admins can rename the team.'}
          onChange={(event) => {
            setName(event.target.value)
            rename.reset()
          }}
          onBlur={() => setTouched(true)}
        />
        {editable && (
          <button type="submit" className="btn btn-primary self-start" disabled={rename.isPending || unchanged}>
            {rename.isPending ? 'Saving…' : 'Save name'}
          </button>
        )}
      </form>
    </section>
  )
}

function MembersSection({ org, userId, manage, isOwner }: { org: OrgSummary; userId: string; manage: boolean; isOwner: boolean }) {
  const members = useMembers(org.id)
  const changeRole = useChangeRole(org.id)
  const remove = useRemoveMember(org.id)
  const [removing, setRemoving] = useState<Member | null>(null)

  /** Admins cannot touch Owners; only an Owner can. */
  const canEdit = (member: Member) => manage && member.userId !== userId && (isOwner || member.role !== 'Owner')

  return (
    <section aria-labelledby="members" className="card mt-6 max-w-xl p-6">
      <h2 id="members" className="text-md text-fg">
        Members
      </h2>
      <p className="mt-1 text-base text-muted">People who can open this team.</p>

      {changeRole.error && <Notice tone="error" className="mt-4">{messageOf(changeRole.error)}</Notice>}

      {members.isPending ? (
        <div className="mt-4 flex flex-col gap-2" role="status" aria-label="Loading members">
          <div className="skeleton h-14" aria-hidden />
          <div className="skeleton h-14" aria-hidden />
        </div>
      ) : members.isError ? (
        <Notice tone="error" className="mt-4">{messageOf(members.error)}</Notice>
      ) : (
        <ul className="mt-4 divide-y divide-line">
          {members.data.map((member) => (
            <li key={member.userId} className="flex flex-wrap items-center gap-3 py-3">
              <div className="min-w-0 flex-1">
                <p className="truncate text-sm font-medium text-fg">
                  {member.name}
                  {member.userId === userId && <span className="ml-2 text-xs font-normal text-muted">you</span>}
                </p>
                <p className="truncate text-xs text-muted">
                  {member.email} · joined {dateOf(member.joinedAt)}
                </p>
              </div>
              {canEdit(member) ? (
                <>
                  <label className="sr-only" htmlFor={`role-${member.userId}`}>
                    Role of {member.name}
                  </label>
                  <select
                    id={`role-${member.userId}`}
                    className="input h-10 w-auto"
                    value={member.role}
                    disabled={changeRole.isPending}
                    onChange={(event) => changeRole.mutate({ userId: member.userId, role: event.target.value as Role })}
                  >
                    {ROLES.filter((role) => isOwner || role !== 'Owner').map((role) => (
                      <option key={role} value={role}>
                        {role}
                      </option>
                    ))}
                  </select>
                  <button type="button" className="btn btn-ghost" onClick={() => setRemoving(member)}>
                    Remove
                  </button>
                </>
              ) : (
                <span className="rounded-full bg-surface px-3 py-1 text-xs font-medium text-fg">{member.role}</span>
              )}
            </li>
          ))}
        </ul>
      )}

      <ConfirmDialog
        open={removing !== null}
        onOpenChange={(open) => {
          if (!open) {
            setRemoving(null)
            remove.reset()
          }
        }}
        title={`Remove ${removing?.name ?? 'member'}?`}
        description={
          <>
            {removing?.email} will lose access to {org.name} straight away. They can be invited again later.
          </>
        }
        confirmLabel={`Remove ${removing?.name ?? 'member'}`}
        pending={remove.isPending}
        error={messageOf(remove.error)}
        onConfirm={() => {
          if (removing) remove.mutate(removing.userId, { onSuccess: () => setRemoving(null) })
        }}
      />
    </section>
  )
}

function InvitesSection({ org, isOwner }: { org: OrgSummary; isOwner: boolean }) {
  const invites = useInvites(org.id, true)
  const create = useCreateInvite(org.id)
  const revoke = useRevokeInvite(org.id)
  const [email, setEmail] = useState('')
  const [role, setRole] = useState<Role>('Viewer')
  const [touched, setTouched] = useState(false)
  const [sentTo, setSentTo] = useState<string | null>(null)

  const emailError = touched ? validateEmail(email) : null
  const failure = create.error instanceof ApiError ? create.error : null

  const submit = (event: FormEvent) => {
    event.preventDefault()
    setTouched(true)
    if (validateEmail(email) !== null) return
    create.mutate(
      { email: email.trim(), role },
      {
        onSuccess: () => {
          setSentTo(email.trim())
          setEmail('')
          setTouched(false)
        },
      },
    )
  }

  return (
    <section aria-labelledby="invites" className="card mt-6 max-w-xl p-6">
      <h2 id="invites" className="text-md text-fg">
        Invite someone
      </h2>
      <p className="mt-1 text-base text-muted">They get an email with a link that works for 7 days.</p>

      <form onSubmit={submit} noValidate className="mt-4 flex flex-col gap-4">
        {failure && <Notice tone="error">{failure.fields.email?.[0] ?? failure.message}</Notice>}
        {sentTo && <Notice tone="success">Invitation sent to {sentTo}.</Notice>}
        <TextField
          label="Email"
          type="email"
          autoComplete="off"
          placeholder="ana@example.com"
          value={email}
          error={emailError}
          onChange={(event) => {
            setEmail(event.target.value)
            setSentTo(null)
          }}
          onBlur={() => setTouched(true)}
        />
        <div className="field">
          <label htmlFor="invite-role" className="field-label">
            Role
          </label>
          <select id="invite-role" className="input" value={role} onChange={(event) => setRole(event.target.value as Role)}>
            {ROLES.filter((r) => isOwner || r !== 'Owner').map((r) => (
              <option key={r} value={r}>
                {r}
              </option>
            ))}
          </select>
          <p className="field-hint">{ROLE_HELP[role]}</p>
        </div>
        <button type="submit" className="btn btn-primary self-start" disabled={create.isPending}>
          {create.isPending ? 'Sending…' : 'Send invitation'}
        </button>
      </form>

      {invites.data && invites.data.length > 0 && (
        <>
          <h3 className="mt-8 text-sm font-semibold text-fg">Waiting for an answer</h3>
          {revoke.error && <Notice tone="error" className="mt-3">{messageOf(revoke.error)}</Notice>}
          <ul className="mt-2 divide-y divide-line">
            {invites.data.map((invite) => (
              <li key={invite.id} className="flex flex-wrap items-center gap-3 py-3">
                <div className="min-w-0 flex-1">
                  <p className="truncate text-sm font-medium text-fg">{invite.email}</p>
                  <p className="text-xs text-muted">
                    {invite.role} · expires {dateOf(invite.expiresAt)}
                  </p>
                </div>
                <button
                  type="button"
                  className="btn btn-ghost"
                  aria-label={`Revoke invitation for ${invite.email}`}
                  disabled={revoke.isPending}
                  onClick={() => revoke.mutate(invite.id)}
                >
                  Revoke
                </button>
              </li>
            ))}
          </ul>
        </>
      )}
    </section>
  )
}

function DangerSection({ org, userId, isOwner }: { org: OrgSummary; userId: string; isOwner: boolean }) {
  const navigate = useNavigate()
  const leave = useRemoveMember(org.id)
  const destroy = useDeleteOrg(org.id)
  const [leaving, setLeaving] = useState(false)
  const [deleting, setDeleting] = useState(false)
  const [typed, setTyped] = useState('')

  return (
    <section aria-labelledby="danger" className="card mt-6 max-w-xl border border-down/30 p-6">
      <h2 id="danger" className="text-md text-fg">
        Leave or delete
      </h2>

      <div className="mt-4 flex flex-wrap items-center justify-between gap-3">
        <p className="min-w-0 flex-1 text-base text-muted">Leave {org.name}. You lose access until someone invites you again.</p>
        <button type="button" className="btn" onClick={() => setLeaving(true)}>
          Leave team
        </button>
      </div>

      {isOwner && (
        <div className="mt-4 flex flex-wrap items-center justify-between gap-3 border-t border-line pt-4">
          <p className="min-w-0 flex-1 text-base text-muted">
            Delete {org.name} with its members and invitations. This cannot be undone.
          </p>
          <button type="button" className="btn" onClick={() => setDeleting(true)}>
            Delete team
          </button>
        </div>
      )}

      <ConfirmDialog
        open={leaving}
        onOpenChange={(open) => {
          setLeaving(open)
          if (!open) leave.reset()
        }}
        title={`Leave ${org.name}?`}
        description="You will lose access to its monitors and status pages."
        confirmLabel="Leave team"
        pending={leave.isPending}
        error={messageOf(leave.error)}
        onConfirm={() =>
          leave.mutate(userId, {
            onSuccess: () => {
              setLeaving(false)
              void navigate('/dashboard')
            },
          })
        }
      />

      <ConfirmDialog
        open={deleting}
        onOpenChange={(open) => {
          setDeleting(open)
          if (!open) {
            setTyped('')
            destroy.reset()
          }
        }}
        title={`Delete ${org.name}?`}
        description="Everything in this team is removed for every member. There is no way back."
        confirmLabel="Delete team"
        confirmDisabled={typed.trim() !== org.name}
        pending={destroy.isPending}
        error={messageOf(destroy.error)}
        onConfirm={() =>
          destroy.mutate(undefined, {
            onSuccess: () => {
              setDeleting(false)
              setTyped('')
              void navigate('/dashboard')
            },
          })
        }
      >
        <TextField
          label={`Type ${org.name} to confirm`}
          autoComplete="off"
          value={typed}
          onChange={(event) => setTyped(event.target.value)}
        />
      </ConfirmDialog>
    </section>
  )
}
