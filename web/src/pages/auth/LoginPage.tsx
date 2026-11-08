import { useState, type FormEvent } from 'react'
import { Link } from 'react-router'
import { TextField } from '@/components/TextField'
import { validateEmail } from '@/lib/validation'
import { AccountsNotice } from './AccountsNotice'

export default function LoginPage() {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [touched, setTouched] = useState({ email: false, password: false })
  const [done, setDone] = useState(false)

  const emailError = touched.email ? validateEmail(email) : null
  const passwordError = touched.password && !password ? 'Enter your password' : null

  const submit = (event: FormEvent) => {
    event.preventDefault()
    setTouched({ email: true, password: true })
    if (validateEmail(email) === null && password) setDone(true)
  }

  return (
    <div className="rise">
      <h1 className="text-2xl font-semibold">Welcome back</h1>
      <p className="mt-1 text-base text-muted">Sign in to see what is up and what is not.</p>

      <form onSubmit={submit} noValidate className="mt-8 flex flex-col gap-5">
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
          aside={<span className="text-xs text-subtle">Reset arrives with accounts</span>}
          onChange={(event) => setPassword(event.target.value)}
          onBlur={() => setTouched((t) => ({ ...t, password: true }))}
        />
        <button type="submit" className="btn btn-primary btn-lg w-full">
          Sign in
        </button>
      </form>

      {done && (
        <div className="mt-5">
          <AccountsNotice action="sign in" />
        </div>
      )}

      <div className="mt-8 flex flex-col gap-3 text-sm text-muted">
        <p>
          New to Pulse?{' '}
          <Link to="/signup" className="font-semibold text-fg underline underline-offset-4">
            Create an account
          </Link>
        </p>
        <p>
          Just looking?{' '}
          <Link to="/dashboard" className="font-semibold text-fg underline underline-offset-4">
            Try the demo
          </Link>
        </p>
      </div>
    </div>
  )
}
