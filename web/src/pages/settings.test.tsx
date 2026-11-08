import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import type { Me, Member } from '@/api/types'
import { ana, mockApi, problem } from '@/test/mockApi'
import { renderAt } from '@/test/render'

const members: Member[] = [
  { userId: 'user-ana', email: 'ana@example.com', name: 'Ana Rakoto', role: 'Owner', joinedAt: '2026-01-05T09:00:00Z' },
  { userId: 'user-bo', email: 'bo@example.com', name: 'Bo Rabe', role: 'Viewer', joinedAt: '2026-02-10T09:00:00Z' },
]

const owner = { 'GET /api/auth/me': { body: ana }, 'GET /api/orgs/org-acme/members': { body: members }, 'GET /api/orgs/org-acme/invites': { body: [] } }

function asRole(role: 'Admin' | 'Viewer'): Me {
  return { ...ana, organizations: [{ ...ana.organizations[0]!, role }] }
}

describe('Settings: team', () => {
  it('lists the members and who can change them', async () => {
    mockApi(owner)
    renderAt('/settings', { demo: false })

    expect(await screen.findByText('Bo Rabe')).toBeInTheDocument()
    expect(screen.getByLabelText('Role of Bo Rabe')).toHaveValue('Viewer')
    // You cannot edit yourself, so Ana has a badge instead of a select.
    expect(screen.queryByLabelText('Role of Ana Rakoto')).not.toBeInTheDocument()
  })

  it('changes a role', async () => {
    const user = userEvent.setup()
    const server = mockApi({ ...owner, 'PATCH /api/orgs/org-acme/members/user-bo': { body: { ...members[1], role: 'Admin' } } })
    renderAt('/settings', { demo: false })

    await user.selectOptions(await screen.findByLabelText('Role of Bo Rabe'), 'Admin')

    await waitFor(() => expect(server.called('PATCH /api/orgs/org-acme/members/user-bo')).toHaveLength(1))
    expect(server.called('PATCH /api/orgs/org-acme/members/user-bo')[0]?.body).toEqual({ role: 'Admin' })
  })

  it('asks before removing someone, then removes them', async () => {
    const user = userEvent.setup()
    const server = mockApi({ ...owner, 'DELETE /api/orgs/org-acme/members/user-bo': { status: 204 } })
    renderAt('/settings', { demo: false })

    await screen.findByText('Bo Rabe')
    const row = screen.getByText('Bo Rabe').closest('li')!
    await user.click(within(row).getByRole('button', { name: 'Remove' }))

    expect(server.called('DELETE /api/orgs/org-acme/members/user-bo')).toHaveLength(0)
    const dialog = await screen.findByRole('dialog', { name: 'Remove Bo Rabe?' })
    await user.click(within(dialog).getByRole('button', { name: 'Remove Bo Rabe' }))

    await waitFor(() => expect(server.called('DELETE /api/orgs/org-acme/members/user-bo')).toHaveLength(1))
  })

  it('shows the reason inside the dialog when the server refuses', async () => {
    const user = userEvent.setup()
    mockApi({
      ...owner,
      'DELETE /api/orgs/org-acme/members/user-bo': { status: 409, body: problem('last_owner', 'A team needs at least one Owner.') },
    })
    renderAt('/settings', { demo: false })

    const row = (await screen.findByText('Bo Rabe')).closest('li')!
    await user.click(within(row).getByRole('button', { name: 'Remove' }))
    const dialog = await screen.findByRole('dialog')
    await user.click(within(dialog).getByRole('button', { name: 'Remove Bo Rabe' }))

    expect(await within(dialog).findByText('A team needs at least one Owner.')).toBeInTheDocument()
  })

  it('sends an invitation and lists it as waiting', async () => {
    const user = userEvent.setup()
    const waiting = { id: 'inv-1', email: 'cy@example.com', role: 'Admin', expiresAt: '2026-11-15T00:00:00Z', createdAt: '2026-11-08T00:00:00Z' }
    let sent = false
    const server = mockApi({
      ...owner,
      'GET /api/orgs/org-acme/invites': () => ({ body: sent ? [waiting] : [] }),
      'POST /api/orgs/org-acme/invites': () => {
        sent = true
        return { status: 201, body: waiting }
      },
    })
    renderAt('/settings', { demo: false })

    await user.type(await screen.findByLabelText('Email'), 'cy@example.com')
    await user.selectOptions(screen.getByLabelText('Role'), 'Admin')
    await user.click(screen.getByRole('button', { name: 'Send invitation' }))

    expect(await screen.findByText('Invitation sent to cy@example.com.')).toBeInTheDocument()
    expect(server.called('POST /api/orgs/org-acme/invites')[0]?.body).toEqual({ email: 'cy@example.com', role: 'Admin' })
    expect(await screen.findByRole('button', { name: 'Revoke invitation for cy@example.com' })).toBeInTheDocument()
  })

  it('renames the team', async () => {
    const user = userEvent.setup()
    const server = mockApi({ ...owner, 'PATCH /api/orgs/org-acme': { body: { ...ana.organizations[0], name: 'Acme Cloud' } } })
    renderAt('/settings', { demo: false })

    const field = await screen.findByLabelText('Team name')
    await user.clear(field)
    await user.type(field, 'Acme Cloud')
    await user.click(screen.getByRole('button', { name: 'Save name' }))

    await waitFor(() => expect(server.called('PATCH /api/orgs/org-acme')).toHaveLength(1))
    expect(server.called('PATCH /api/orgs/org-acme')[0]?.body).toEqual({ name: 'Acme Cloud' })
  })

  it('only deletes the team after its name is typed', async () => {
    const user = userEvent.setup()
    const server = mockApi({ ...owner, 'DELETE /api/orgs/org-acme': { status: 204 } })
    renderAt('/settings', { demo: false })

    await user.click(await screen.findByRole('button', { name: 'Delete team' }))
    const dialog = await screen.findByRole('dialog', { name: 'Delete Acme Platform?' })
    const confirm = within(dialog).getByRole('button', { name: 'Delete team' })
    expect(confirm).toBeDisabled()

    await user.type(within(dialog).getByLabelText('Type Acme Platform to confirm'), 'Acme Platform')
    expect(confirm).toBeEnabled()
    await user.click(confirm)

    await waitFor(() => expect(server.called('DELETE /api/orgs/org-acme')).toHaveLength(1))
  })
})

describe('Settings: roles limit what is offered', () => {
  it('lets an Admin manage members but not delete the team or touch an Owner', async () => {
    mockApi({ ...owner, 'GET /api/auth/me': { body: asRole('Admin') } })
    renderAt('/settings', { demo: false })

    expect(await screen.findByLabelText('Role of Bo Rabe')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Delete team' })).not.toBeInTheDocument()
    // The Owner row is a badge, not a select.
    expect(screen.queryByLabelText('Role of Ana Rakoto')).not.toBeInTheDocument()
    const options = within(screen.getByLabelText('Role of Bo Rabe')).getAllByRole('option').map((o) => o.textContent)
    expect(options).toEqual(['Admin', 'Viewer'])
  })

  it('gives a Viewer a read-only view with no invite form', async () => {
    mockApi({ ...owner, 'GET /api/auth/me': { body: asRole('Viewer') } })
    renderAt('/settings', { demo: false })

    expect(await screen.findByText('Bo Rabe')).toBeInTheDocument()
    expect(screen.queryByText('Invite someone')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Remove' })).not.toBeInTheDocument()
    expect(screen.getByLabelText('Team name')).toBeDisabled()
    expect(screen.queryByRole('button', { name: 'Save name' })).not.toBeInTheDocument()
  })

  it('shows the demo only appearance settings', async () => {
    renderAt('/settings')

    expect(await screen.findByRole('heading', { name: 'Appearance' })).toBeInTheDocument()
    expect(screen.queryByText('Members')).not.toBeInTheDocument()
  })
})

describe('Team switcher', () => {
  it('switches between teams and signs out', async () => {
    const user = userEvent.setup()
    const two: Me = {
      ...ana,
      organizations: [...ana.organizations, { id: 'org-two', name: 'Side Project', slug: 'side-project', role: 'Admin' }],
    }
    const server = mockApi({
      'GET /api/auth/me': { body: two },
      'POST /api/auth/logout': { status: 204 },
    })
    const { router } = renderAt('/dashboard', { demo: false })

    await user.click(await screen.findByRole('button', { name: /Team: Acme Platform/ }))
    await user.click(await screen.findByRole('menuitem', { name: /Side Project/ }))
    expect(await screen.findByRole('button', { name: /Team: Side Project/ })).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Log out' }))
    await waitFor(() => expect(router.state.location.pathname).toBe('/'))
    expect(server.called('POST /api/auth/logout')).toHaveLength(1)
  })
})
