import { MailCheck } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { Link } from 'react-router'
import { ApiError } from '@/api/client'
import { useForgotPassword } from '@/api/hooks'
import { Notice } from '@/components/Notice'
import { TextField } from '@/components/TextField'
import { validateEmail } from '@/lib/validation'

export default function ForgotPasswordPage() {
  const forgot = useForgotPassword()
  const [email, setEmail] = useState('')
  const [touched, setTouched] = useState(false)
  const error = touched ? validateEmail(email) : null

  const submit = (event: FormEvent) => {
    event.preventDefault()
    setTouched(true)
    if (validateEmail(email) === null) forgot.mutate(email.trim())
  }

  if (forgot.isSuccess) {
    return (
      <div className="rise">
        <span className="flex size-12 items-center justify-center rounded-full bg-surface text-fg">
          <MailCheck size={20} strokeWidth={1.5} aria-hidden />
        </span>
        <h1 className="mt-6 text-2xl font-semibold">Check your email</h1>
        <p className="mt-2 text-base text-muted">
          If <span className="font-semibold text-fg">{email.trim()}</span> has a Pulse account, a link to choose a new
          password is on its way.
        </p>
        <Link to="/login" className="btn btn-lg mt-6 w-full">
          Back to sign in
        </Link>
      </div>
    )
  }

  return (
    <div className="rise">
      <h1 className="text-2xl font-semibold">Reset your password</h1>
      <p className="mt-1 text-base text-muted">Enter your email and we will send you a link to choose a new one.</p>

      <form onSubmit={submit} noValidate className="mt-8 flex flex-col gap-5">
        {forgot.error instanceof ApiError && <Notice tone="error">{forgot.error.message}</Notice>}
        <TextField
          label="Email"
          type="email"
          autoComplete="email"
          placeholder="you@company.com"
          value={email}
          error={error}
          onChange={(event) => setEmail(event.target.value)}
          onBlur={() => setTouched(true)}
        />
        <button type="submit" className="btn btn-primary btn-lg w-full" disabled={forgot.isPending}>
          {forgot.isPending ? 'Sending…' : 'Send reset link'}
        </button>
      </form>

      <p className="mt-8 text-sm text-muted">
        Remembered it?{' '}
        <Link to="/login" className="font-semibold text-fg underline underline-offset-4">
          Sign in
        </Link>
      </p>
    </div>
  )
}
