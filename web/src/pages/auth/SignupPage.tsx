import { MailCheck } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { Link, useSearchParams } from 'react-router'
import { ApiError } from '@/api/client'
import { useRegister, useResendVerification } from '@/api/hooks'
import { Notice } from '@/components/Notice'
import { TextField } from '@/components/TextField'
import { safeNext } from '@/lib/redirect'
import {
  STRENGTH_LABEL,
  passwordStrength,
  slugify,
  validateEmail,
  validateName,
  validatePassword,
} from '@/lib/validation'

type Field = 'name' | 'org' | 'email' | 'password'

const METER_COLOR = ['', 'var(--status-down)', 'var(--status-degraded)', 'var(--status-maintenance)', 'var(--status-up)']

/** The API names the team field `teamName`; the form calls it `org`. */
const SERVER_FIELD: Record<string, Field> = { name: 'name', teamName: 'org', email: 'email', password: 'password' }

function CheckInbox({ email }: { email: string }) {
  const resend = useResendVerification()

  return (
    <div className="rise">
      <span className="flex size-12 items-center justify-center rounded-full bg-surface text-fg">
        <MailCheck size={20} strokeWidth={1.5} aria-hidden />
      </span>
      <h1 className="mt-6 text-2xl font-semibold">Check your email</h1>
      <p className="mt-2 text-base text-muted">
        We sent a confirmation link to <span className="font-semibold text-fg">{email}</span>. Open it to finish setting up
        your account, then sign in.
      </p>
      <div className="mt-6 flex flex-col gap-3">
        {resend.isSuccess && <Notice tone="success">Sent again. It can take a minute to arrive.</Notice>}
        {resend.isError && <Notice tone="error">Could not send it. Try again in a moment.</Notice>}
        <button type="button" className="btn btn-lg w-full" disabled={resend.isPending} onClick={() => resend.mutate(email)}>
          {resend.isPending ? 'Sending…' : 'Send it again'}
        </button>
        <Link to={`/login?email=${encodeURIComponent(email)}`} className="btn btn-primary btn-lg w-full">
          Go to sign in
        </Link>
      </div>
    </div>
  )
}

export default function SignupPage() {
  const [params] = useSearchParams()
  const register = useRegister()
  const [values, setValues] = useState<Record<Field, string>>({ name: '', org: '', email: params.get('email') ?? '', password: '' })
  const [touched, setTouched] = useState<Record<Field, boolean>>({ name: false, org: false, email: false, password: false })
  const [sentTo, setSentTo] = useState<string | null>(null)

  const next = safeNext(params.get('next'))
  const suffix = next === '/dashboard' ? '' : `?next=${encodeURIComponent(next)}`

  const errors: Record<Field, string | null> = {
    name: validateName(values.name, 'your name'),
    org: validateName(values.org, 'a team name'),
    email: validateEmail(values.email),
    password: validatePassword(values.password),
  }

  // Problems only the server can see (for example a password it rejects) show under the same fields.
  const serverErrors: Partial<Record<Field, string>> = {}
  if (register.error instanceof ApiError) {
    for (const [key, messages] of Object.entries(register.error.fields)) {
      const field = SERVER_FIELD[key]
      if (field && messages[0]) serverErrors[field] = messages[0]
    }
  }
  const formError = register.error instanceof ApiError && Object.keys(serverErrors).length === 0 ? register.error.message : null

  const shown = (field: Field) => (touched[field] ? errors[field] : null) ?? serverErrors[field] ?? null
  const strength = passwordStrength(values.password)
  const slug = slugify(values.org)

  const set = (field: Field) => ({
    value: values[field],
    onChange: (event: React.ChangeEvent<HTMLInputElement>) => setValues((v) => ({ ...v, [field]: event.target.value })),
    onBlur: () => setTouched((t) => ({ ...t, [field]: true })),
    error: shown(field),
  })

  const submit = (event: FormEvent) => {
    event.preventDefault()
    setTouched({ name: true, org: true, email: true, password: true })
    if (!Object.values(errors).every((e) => e === null)) return
    const email = values.email.trim()
    register.mutate(
      { name: values.name.trim(), teamName: values.org.trim(), email, password: values.password },
      { onSuccess: () => setSentTo(email) },
    )
  }

  if (sentTo) return <CheckInbox email={sentTo} />

  return (
    <div className="rise">
      <h1 className="text-2xl font-semibold">Create your account</h1>
      <p className="mt-1 text-base text-muted">Add a URL and see it monitored within a minute.</p>

      <form onSubmit={submit} noValidate className="mt-8 flex flex-col gap-5">
        {formError && <Notice tone="error">{formError}</Notice>}
        <TextField label="Your name" autoComplete="name" placeholder="Colombe" {...set('name')} />
        <TextField
          label="Team name"
          autoComplete="organization"
          placeholder="Acme Platform"
          hint={slug ? <>Your status page will live at <span className="mono">/status/{slug}</span></> : 'Shown on your status page and in alerts'}
          {...set('org')}
        />
        <TextField label="Work email" type="email" autoComplete="email" placeholder="you@company.com" {...set('email')} />
        <div>
          <TextField
            label="Password"
            type="password"
            autoComplete="new-password"
            hint="At least 10 characters. A few random words work well."
            {...set('password')}
          />
          <div className="mt-2 flex items-center gap-3" aria-hidden={strength === 0}>
            <div className="flex flex-1 gap-1">
              {[1, 2, 3, 4].map((step) => (
                <span
                  key={step}
                  className="h-1.5 flex-1 rounded-full bg-surface-2 transition-colors"
                  style={step <= strength ? { background: METER_COLOR[strength] } : undefined}
                />
              ))}
            </div>
            <span className="w-12 text-xs text-muted" role="status">{STRENGTH_LABEL[strength]}</span>
          </div>
        </div>
        <button type="submit" className="btn btn-primary btn-lg w-full" disabled={register.isPending}>
          {register.isPending ? 'Creating account…' : 'Create account'}
        </button>
      </form>

      <div className="mt-8 flex flex-col gap-3 text-sm text-muted">
        <p>
          Already have an account?{' '}
          <Link to={`/login${suffix}`} className="font-semibold text-fg underline underline-offset-4">
            Sign in
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
