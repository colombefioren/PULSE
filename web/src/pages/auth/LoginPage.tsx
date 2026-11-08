import { useState, type FormEvent } from 'react'
import { Link, useLocation, useNavigate, useSearchParams } from 'react-router'
import { ApiError } from '@/api/client'
import { useLogin, useResendVerification } from '@/api/hooks'
import { Notice } from '@/components/Notice'
import { TextField } from '@/components/TextField'
import { safeNext } from '@/lib/redirect'
import { validateEmail } from '@/lib/validation'

export default function LoginPage() {
  const navigate = useNavigate()
  const location = useLocation()
  const [params] = useSearchParams()
  const login = useLogin()
  const resend = useResendVerification()

  const [email, setEmail] = useState(params.get('email') ?? '')
  const [password, setPassword] = useState('')
  const [touched, setTouched] = useState({ email: false, password: false })

  const next = safeNext(params.get('next') ?? (location.state as { from?: string } | null)?.from)
  const suffix = next === '/dashboard' ? '' : `?next=${encodeURIComponent(next)}`

  const emailError = touched.email ? validateEmail(email) : null
  const passwordError = touched.password && !password ? 'Enter your password' : null
  const failure = login.error instanceof ApiError ? login.error : null

  const submit = (event: FormEvent) => {
    event.preventDefault()
    setTouched({ email: true, password: true })
    if (validateEmail(email) !== null || !password) return
    login.mutate({ email: email.trim(), password, remember: true }, { onSuccess: () => void navigate(next, { replace: true }) })
  }

  return (
    <div className="rise">
      <h1 className="text-2xl font-semibold">Welcome back</h1>
      <p className="mt-1 text-base text-muted">Sign in to see what is up and what is not.</p>

      <form onSubmit={submit} noValidate className="mt-8 flex flex-col gap-5">
        {failure && failure.code === 'email_not_verified' ? (
          <Notice tone="error">
            <p>{failure.message}</p>
            {resend.isSuccess ? (
              <p className="mt-2 font-medium">Sent. Check {email.trim()} for a new link.</p>
            ) : (
              <button
                type="button"
                className="mt-2 font-semibold underline underline-offset-4"
                disabled={resend.isPending}
                onClick={() => resend.mutate(email.trim())}
              >
                {resend.isPending ? 'Sending…' : 'Send the confirmation email again'}
              </button>
            )}
          </Notice>
        ) : failure ? (
          <Notice tone="error">{failure.message}</Notice>
        ) : null}

        <TextField
          label="Email"
          type="email"
          autoComplete="email"
          placeholder="you@company.com"
          value={email}
          error={emailError}
          onChange={(event) => setEmail(event.target.value)}
          onBlur={() => setTouched((t) => ({ ...t, email: true }))}
        />
        <TextField
          label="Password"
          type="password"
          autoComplete="current-password"
          value={password}
          error={passwordError}
          aside={
            <Link to="/forgot-password" className="text-xs text-muted underline underline-offset-4 hover:text-fg">
              Forgot password?
            </Link>
          }
          onChange={(event) => setPassword(event.target.value)}
          onBlur={() => setTouched((t) => ({ ...t, password: true }))}
        />
        <button type="submit" className="btn btn-primary btn-lg w-full" disabled={login.isPending}>
          {login.isPending ? 'Signing in…' : 'Sign in'}
        </button>
      </form>

      <div className="mt-8 flex flex-col gap-3 text-sm text-muted">
        <p>
          New to Pulse?{' '}
          <Link to={`/signup${suffix}`} className="font-semibold text-fg underline underline-offset-4">
            Create an account
          </Link>
        </p>
        <p>
          Just looking?{' '}
          <Link to="/demo" className="font-semibold text-fg underline underline-offset-4">
            Try the demo
          </Link>
        </p>
      </div>
    </div>
  )
}
