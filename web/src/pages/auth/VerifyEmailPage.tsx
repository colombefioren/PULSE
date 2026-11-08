import { CircleCheck, CircleX } from 'lucide-react'
import { useEffect, useRef, useState, type FormEvent } from 'react'
import { Link, useSearchParams } from 'react-router'
import { useResendVerification, useVerifyEmail } from '@/api/hooks'
import { Notice } from '@/components/Notice'
import { TextField } from '@/components/TextField'
import { validateEmail } from '@/lib/validation'

function Resend() {
  const resend = useResendVerification()
  const [email, setEmail] = useState('')
  const [touched, setTouched] = useState(false)
  const error = touched ? validateEmail(email) : null

  const submit = (event: FormEvent) => {
    event.preventDefault()
    setTouched(true)
    if (validateEmail(email) === null) resend.mutate(email.trim())
  }

  if (resend.isSuccess) return <Notice tone="success" className="mt-6">If that address needs confirming, a new link is on its way.</Notice>

  return (
    <form onSubmit={submit} noValidate className="mt-6 flex flex-col gap-4">
      <TextField
        label="Email"
        type="email"
        autoComplete="email"
        value={email}
        error={error}
        onChange={(event) => setEmail(event.target.value)}
        onBlur={() => setTouched(true)}
      />
      <button type="submit" className="btn btn-lg w-full" disabled={resend.isPending}>
        {resend.isPending ? 'Sending…' : 'Send a new link'}
      </button>
    </form>
  )
}

export default function VerifyEmailPage() {
  const [params] = useSearchParams()
  const verify = useVerifyEmail()
  const started = useRef(false)
  const userId = params.get('userId')
  const token = params.get('token')

  // The link is single-use, so confirm exactly once even if React runs the effect twice in development.
  useEffect(() => {
    if (started.current || !userId || !token) return
    started.current = true
    verify.mutate({ userId, token })
  }, [userId, token, verify])

  if (verify.isSuccess) {
    return (
      <div className="rise">
        <span className="flex size-12 items-center justify-center rounded-full bg-surface text-up">
          <CircleCheck size={20} strokeWidth={1.5} aria-hidden />
        </span>
        <h1 className="mt-6 text-2xl font-semibold">Email confirmed</h1>
        <p className="mt-2 text-base text-muted">Your account is ready. Sign in to continue.</p>
        <Link to="/login" className="btn btn-primary btn-lg mt-6 w-full">
          Sign in
        </Link>
      </div>
    )
  }

  if (!userId || !token || verify.isError) {
    return (
      <div className="rise">
        <span className="flex size-12 items-center justify-center rounded-full bg-surface text-down">
          <CircleX size={20} strokeWidth={1.5} aria-hidden />
        </span>
        <h1 className="mt-6 text-2xl font-semibold">This link did not work</h1>
        <p className="mt-2 text-base text-muted">
          It may have expired or already been used. If you already confirmed your email, you can{' '}
          <Link to="/login" className="font-semibold text-fg underline underline-offset-4">
            sign in
          </Link>
          . Otherwise, ask for a new one.
        </p>
        <Resend />
      </div>
    )
  }

  return (
    <div className="rise" role="status">
      <h1 className="text-2xl font-semibold">Confirming your email…</h1>
      <div className="skeleton mt-6 h-10" aria-hidden />
    </div>
  )
}
