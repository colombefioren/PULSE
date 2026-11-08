import { useState, type FormEvent } from 'react'
import { Link } from 'react-router'
import { TextField } from '@/components/TextField'
import {
  STRENGTH_LABEL,
  passwordStrength,
  slugify,
  validateEmail,
  validateName,
  validatePassword,
} from '@/lib/validation'
import { AccountsNotice } from './AccountsNotice'

type Field = 'name' | 'org' | 'email' | 'password'

const METER_COLOR = ['', 'var(--status-down)', 'var(--status-degraded)', 'var(--status-maintenance)', 'var(--status-up)']

export default function SignupPage() {
  const [values, setValues] = useState<Record<Field, string>>({ name: '', org: '', email: '', password: '' })
  const [touched, setTouched] = useState<Record<Field, boolean>>({ name: false, org: false, email: false, password: false })
  const [done, setDone] = useState(false)

  const errors: Record<Field, string | null> = {
    name: validateName(values.name, 'your name'),
    org: validateName(values.org, 'a team name'),
    email: validateEmail(values.email),
    password: validatePassword(values.password),
  }
  const shown = (field: Field) => (touched[field] ? errors[field] : null)
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
    if (Object.values(errors).every((e) => e === null)) setDone(true)
  }

  return (
    <div className="rise">
      <h1 className="text-2xl font-semibold">Create your account</h1>
      <p className="mt-1 text-base text-muted">Add a URL and see it monitored within a minute.</p>

      <form onSubmit={submit} noValidate className="mt-8 flex flex-col gap-5">
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
        <button type="submit" className="btn btn-primary btn-lg w-full">
          Create account
        </button>
      </form>

      {done && (
        <div className="mt-5">
          <AccountsNotice action="sign up" />
        </div>
      )}

      <div className="mt-8 flex flex-col gap-3 text-sm text-muted">
        <p>
          Already have an account?{' '}
          <Link to="/login" className="font-semibold text-fg underline underline-offset-4">
            Sign in
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
