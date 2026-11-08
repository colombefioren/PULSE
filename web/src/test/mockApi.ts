import { vi } from 'vitest'
import { resetCsrf } from '@/api/client'
import type { Me } from '@/api/types'

export interface MockReply {
  status?: number
  body?: unknown
}

export interface RecordedCall {
  method: string
  path: string
  body: unknown
  csrf: string | null
}

type Handler = MockReply | ((call: RecordedCall) => MockReply)

/**
 * Replaces `fetch` with an in-memory API. Keys look like `POST /api/auth/login`; anything unlisted answers 404.
 * The antiforgery endpoint always works, so tests only describe what they care about.
 */
export function mockApi(handlers: Record<string, Handler> = {}) {
  const calls: RecordedCall[] = []
  const table = { ...handlers }

  const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
    const method = (init?.method ?? 'GET').toUpperCase()
    const path = typeof input === 'string' ? input : input instanceof URL ? input.pathname : input.url
    const headers = new Headers(init?.headers)
    const call: RecordedCall = {
      method,
      path,
      body: typeof init?.body === 'string' ? (JSON.parse(init.body) as unknown) : undefined,
      csrf: headers.get('X-XSRF-TOKEN'),
    }
    calls.push(call)

    const key = `${method} ${path}`
    const handler = table[key] ?? (path === '/api/auth/csrf' ? { body: { token: 'test-token' } } : undefined)
    const reply: MockReply = !handler
      ? { status: 404, body: { title: `No mock for ${key}`, code: 'not_mocked' } }
      : typeof handler === 'function'
        ? handler(call)
        : handler
    const status = reply.status ?? 200
    return Promise.resolve(
      new Response(status === 204 || reply.body === undefined ? null : JSON.stringify(reply.body), {
        status,
        headers: { 'Content-Type': status >= 400 ? 'application/problem+json' : 'application/json' },
      }),
    )
  })

  resetCsrf()
  vi.stubGlobal('fetch', fetchMock)

  return {
    calls,
    /** Add or replace a handler after the app has started. */
    on(key: string, handler: Handler) {
      table[key] = handler
    },
    called(key: string) {
      return calls.filter((call) => `${call.method} ${call.path}` === key)
    },
  }
}

export const ana: Me = {
  id: 'user-ana',
  email: 'ana@example.com',
  name: 'Ana Rakoto',
  emailConfirmed: true,
  organizations: [{ id: 'org-acme', name: 'Acme Platform', slug: 'acme-platform', role: 'Owner' }],
}

/** A problem+json body the way the API sends it. */
export function problem(code: string, title: string, errors?: Record<string, string[]>): { title: string; code: string; errors?: Record<string, string[]> } {
  return { title, code, ...(errors ? { errors } : {}) }
}
