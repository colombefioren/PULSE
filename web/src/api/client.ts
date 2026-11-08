/**
 * Thin fetch wrapper for the Pulse API.
 *
 * Sign-in is an HTTP-only cookie, so requests only need `credentials: 'same-origin'` (the default). Anything
 * that changes data also sends an antiforgery token, which is fetched once and kept in memory.
 */

export class ApiError extends Error {
  readonly status: number
  /** Stable machine-readable code from the API, for example `invalid_credentials`. */
  readonly code: string
  /** Messages per field, keyed by the request's JSON property names. */
  readonly fields: Record<string, string[]>

  constructor(status: number, code: string, message: string, fields: Record<string, string[]> = {}) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.code = code
    this.fields = fields
  }
}

interface Problem {
  title?: string
  code?: string
  errors?: Record<string, string[]>
}

let csrfToken: string | null = null

/** Forget the token. Call after signing in or out: tokens are tied to who is signed in. */
export function resetCsrf() {
  csrfToken = null
}

async function ensureCsrf(): Promise<string> {
  if (csrfToken) return csrfToken
  const response = await fetch('/api/auth/csrf', { headers: { Accept: 'application/json' } })
  if (!response.ok) throw new ApiError(response.status, 'csrf_unavailable', 'Could not reach the server. Try again.')
  csrfToken = ((await response.json()) as { token: string }).token
  return csrfToken
}

async function toError(response: Response): Promise<ApiError> {
  let problem: Problem = {}
  try {
    problem = (await response.json()) as Problem
  } catch {
    // Not JSON (for example a proxy error page): fall back to the status text below.
  }
  return new ApiError(
    response.status,
    problem.code ?? (response.status === 401 ? 'not_signed_in' : 'request_failed'),
    problem.title ?? 'Something went wrong. Try again.',
    problem.errors ?? {},
  )
}

/** What a request carries. `file` is sent as is, with its own content type; anything else is sent as JSON. */
interface Payload {
  json?: unknown
  file?: Blob
  /** Public endpoints are open to visitors without a session, so they take no antiforgery token. */
  anonymous?: boolean
}

async function send<T>(method: string, path: string, payload: Payload, retried = false): Promise<T> {
  const unsafe = method !== 'GET' && !payload.anonymous
  const headers: Record<string, string> = { Accept: 'application/json' }
  let body: BodyInit | undefined
  if (payload.file) {
    headers['Content-Type'] = payload.file.type
    body = payload.file
  } else if (payload.json !== undefined) {
    headers['Content-Type'] = 'application/json'
    body = JSON.stringify(payload.json)
  }
  if (unsafe) headers['X-XSRF-TOKEN'] = await ensureCsrf()

  let response: Response
  try {
    response = await fetch(path, { method, headers, body })
  } catch {
    throw new ApiError(0, 'network', 'Could not reach the server. Check your connection and try again.')
  }

  if (!response.ok) {
    const error = await toError(response)
    // A stale token (after a sign-in, or a server restart) is fixed by fetching a new one once.
    if (error.code === 'csrf_invalid' && !retried) {
      resetCsrf()
      return send<T>(method, path, payload, true)
    }
    throw error
  }

  if (response.status === 204 || response.status === 202) return undefined as T
  const text = await response.text()
  return (text ? JSON.parse(text) : undefined) as T
}

export const api = {
  get: <T>(path: string) => send<T>('GET', path, {}),
  post: <T = void>(path: string, body?: unknown) => send<T>('POST', path, { json: body ?? {} }),
  put: <T = void>(path: string, body?: unknown) => send<T>('PUT', path, { json: body ?? {} }),
  patch: <T = void>(path: string, body?: unknown) => send<T>('PATCH', path, { json: body ?? {} }),
  delete: <T = void>(path: string) => send<T>('DELETE', path, {}),
  /** Uploads a file as the request body, for example a logo. */
  putFile: <T = void>(path: string, file: Blob) => send<T>('PUT', path, { file }),
  /** A POST from a visitor of a public page: no session, no antiforgery token. */
  postPublic: <T = void>(path: string, body?: unknown) => send<T>('POST', path, { json: body ?? {}, anonymous: true }),
}
