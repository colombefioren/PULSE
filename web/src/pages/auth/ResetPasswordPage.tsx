import { CircleCheck } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { Link, useSearchParams } from 'react-router'
import { ApiError } from '@/api/client'
import { useResetPassword } from '@/api/hooks'
import { Notice } from '@/components/Notice'
import { TextField } from '@/components/TextField'
import { validatePassword } from '@/lib/validation'

export default function ResetPasswordPage() {
  const [params] = useSearchParams()
  const reset = useResetPassword()
  const [password, setPassword] = useState('')
  const [touched, setTouched] = useState(false)
  const userId = params.get('userId')
  const token = params.get('token')

  const clientError = touched ? validatePassword(password) : null
  const failure = reset.error instanceof ApiError ? reset.error : null
  const fieldError = failure?.fields.password?.[0] ?? null

  const submit = (event: FormEvent) => {
    event.preventDefault()
    setTouched(true)
    if (!userId || !token || validatePassword(password) !== null) return
    reset.mutate({ userId, token, password })
  }

  if (reset.isSuccess) {
    return (
      <div className="rise">
        <span className="flex size-12 items-center justify-center rounded-full bg-surface text-up">
          <CircleCheck size={20} strokeWidth={1.5} aria-hidden />
        </span>
        <h1 className="mt-6 text-2xl font-semibold">Password changed</h1>
        <p className="mt-2 text-base text-muted">Sign in with your new password.</p>
        <Link to="/login" className="btn btn-primary btn-lg mt-6 w-full">
          Sign in
        </Link>
      </div>
    )
  }

  if (!userId || !token || failure?.code === 'invalid_token') {
    return (
      <div className="rise">
        <h1 className="text-2xl font-semibold">This reset link did not work</h1>
        <p className="mt-2 text-base text-muted">It may have expired or already been used. Ask for a new one.</p>
        <Link to="/forgot-password" className="btn btn-primary btn-lg mt-6 w-full">
          Send a new link
        </Link>
      </div>
    )
  }

  return (
    <div className="rise">
      <h1 className="text-2xl font-semibold">Choose a new password</h1>
      <p className="mt-1 text-base text-muted">Use at least 10 characters. A few random words work well.</p>

      <form onSubmit={submit} noValidate className="mt-8 flex flex-col gap-5">
        {failure && !fieldError && <Notice tone="error">{failure.message}</Notice>}
        <TextField
          label="New password"
          type="password"
          autoComplete="new-password"
          value={password}
          error={clientError ?? fieldError}
          onChange={(event) => setPassword(event.target.value)}
          onBlur={() => setTouched(true)}
        />
        <button type="submit" className="btn btn-primary btn-lg w-full" disabled={reset.isPending}>
          {reset.isPending ? 'Saving…' : 'Change password'}
        </button>
      </form>
    </div>
  )
}
