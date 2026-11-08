import { useNavigate, useParams, Link } from 'react-router'
import { ApiError } from '@/api/client'
import { useAcceptInvite, useInvitePreview, useLogout } from '@/api/hooks'
import { Notice } from '@/components/Notice'
import { useSession } from '@/session/useSession'

export default function InvitePage() {
  const { token = '' } = useParams()
  const navigate = useNavigate()
  const session = useSession()
  const preview = useInvitePreview(token)
  const accept = useAcceptInvite()
  const logout = useLogout()

  if (preview.isPending || session.status === 'loading') {
    return (
      <div className="rise" role="status" aria-label="Loading invitation">
        <div className="skeleton h-8 w-2/3" aria-hidden />
        <div className="skeleton mt-4 h-20" aria-hidden />
      </div>
    )
  }

  if (preview.isError) {
    const message = preview.error instanceof ApiError ? preview.error.message : 'This invitation could not be loaded.'
    return (
      <div className="rise">
        <h1 className="text-2xl font-semibold">This invitation does not work</h1>
        <p className="mt-2 text-base text-muted">{message}</p>
        <Link to="/login" className="btn btn-lg mt-6 w-full">
          Go to sign in
        </Link>
      </div>
    )
  }

  const { organizationName, email, role } = preview.data
  const here = `/invites/${token}`
  const next = encodeURIComponent(here)
  const failure = accept.error instanceof ApiError ? accept.error : null

  const join = () =>
    accept.mutate(token, {
      onSuccess: (org) => {
        if (session.status === 'ready') session.selectOrg(org.id)
        void navigate('/dashboard', { replace: true })
      },
    })

  return (
    <div className="rise">
      <h1 className="text-2xl font-semibold">Join {organizationName}</h1>
      <p className="mt-2 text-base text-muted">
        You were invited as <span className="font-semibold text-fg">{role}</span>. The invitation is for{' '}
        <span className="font-semibold text-fg">{email}</span>.
      </p>

      {session.status === 'ready' && session.mode === 'user' ? (
        <div className="mt-6 flex flex-col gap-3">
          {failure && (
            <Notice tone="error">
              <p>{failure.message}</p>
              {failure.code === 'invite_wrong_account' && (
                <button
                  type="button"
                  className="mt-2 font-semibold underline underline-offset-4"
                  onClick={() => logout.mutate(undefined, { onSuccess: () => void navigate(`/login?next=${next}&email=${encodeURIComponent(email)}`) })}
                >
                  Sign out and use {email}
                </button>
              )}
            </Notice>
          )}
          <button type="button" className="btn btn-primary btn-lg w-full" disabled={accept.isPending} onClick={join}>
            {accept.isPending ? 'Joining…' : `Join ${organizationName}`}
          </button>
          <p className="text-sm text-muted">You are signed in as {session.user.email}.</p>
        </div>
      ) : (
        <div className="mt-6 flex flex-col gap-3">
          <Link to={`/signup?next=${next}&email=${encodeURIComponent(email)}`} className="btn btn-primary btn-lg w-full">
            Create an account
          </Link>
          <Link to={`/login?next=${next}&email=${encodeURIComponent(email)}`} className="btn btn-lg w-full">
            I already have an account
          </Link>
        </div>
      )}
    </div>
  )
}
