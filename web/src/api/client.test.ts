import { describe, expect, it } from 'vitest'
import { mockApi, problem } from '@/test/mockApi'
import { api, ApiError, resetCsrf } from './client'

describe('api client', () => {
  it('sends the antiforgery token on writes and fetches it only once', async () => {
    const server = mockApi({ 'POST /api/things': { status: 204 } })

    await api.post('/api/things', { a: 1 })
    await api.post('/api/things', { a: 2 })

    expect(server.called('GET /api/auth/csrf')).toHaveLength(1)
    expect(server.called('POST /api/things').map((call) => call.csrf)).toEqual(['test-token', 'test-token'])
    expect(server.called('POST /api/things')[0]?.body).toEqual({ a: 1 })
  })

  it('does not ask for a token on reads', async () => {
    const server = mockApi({ 'GET /api/things': { body: [1, 2] } })

    await expect(api.get('/api/things')).resolves.toEqual([1, 2])

    expect(server.called('GET /api/auth/csrf')).toHaveLength(0)
  })

  it('turns a problem response into an ApiError with its code and field messages', async () => {
    mockApi({
      'POST /api/things': { status: 400, body: problem('validation_failed', 'Check the form.', { email: ['Already used'] }) },
    })

    const error = await api.post('/api/things', {}).catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect(error).toMatchObject({ status: 400, code: 'validation_failed', message: 'Check the form.', fields: { email: ['Already used'] } })
  })

  it('refreshes the token once when the server says it is stale, then retries', async () => {
    let attempts = 0
    let tokens = 0
    const server = mockApi({
      'GET /api/auth/csrf': () => ({ body: { token: `token-${++tokens}` } }),
      'POST /api/things': () => (++attempts === 1 ? { status: 400, body: problem('csrf_invalid', 'Stale token.') } : { status: 204 }),
    })

    await api.post('/api/things', {})

    expect(server.called('POST /api/things').map((call) => call.csrf)).toEqual(['token-1', 'token-2'])
  })

  it('fetches a new token after resetCsrf', async () => {
    const server = mockApi({ 'POST /api/things': { status: 204 } })

    await api.post('/api/things')
    resetCsrf()
    await api.post('/api/things')

    expect(server.called('GET /api/auth/csrf')).toHaveLength(2)
  })
})
