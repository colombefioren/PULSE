import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { leaveDemo } from '@/session/demoMode'
import { api, ApiError, resetCsrf } from './client'
import type { Invite, InvitePreview, Me, Member, OrgDetail, OrgSummary, RegisterInput, Role } from './types'

export const ME_KEY = ['me'] as const

/** The signed-in user, or null when nobody is. A 401 is an answer here, not an error. */
export async function fetchMe(): Promise<Me | null> {
  try {
    return await api.get<Me>('/api/auth/me')
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) return null
    throw error
  }
}

/** The shared demo account on a server that has one. Its team is read-only and watches real public websites. */
export const DEMO_ACCOUNT_EMAIL = 'demo@pulse.invalid'

export function useMe() {
  return useQuery<Me | null>({
    queryKey: ME_KEY,
    queryFn: fetchMe,
    retry: false,
    staleTime: 60_000,
  })
}

export function useRegister() {
  return useMutation({ mutationFn: (input: RegisterInput) => api.post('/api/auth/register', input) })
}

export function useLogin() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (input: { email: string; password: string; remember: boolean }) => api.post('/api/auth/login', input),
    onSuccess: async () => {
      // A different identity means different data and a different antiforgery token.
      resetCsrf()
      leaveDemo()
      client.clear()
      await client.fetchQuery({ queryKey: ME_KEY, queryFn: () => api.get<Me>('/api/auth/me') })
    },
  })
}

export function useLogout() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: () => api.post('/api/auth/logout'),
    onSettled: () => {
      resetCsrf()
      leaveDemo()
      client.clear()
      client.setQueryData(ME_KEY, null)
    },
  })
}

export function useVerifyEmail() {
  return useMutation({ mutationFn: (input: { userId: string; token: string }) => api.post('/api/auth/verify-email', input) })
}

export function useResendVerification() {
  return useMutation({ mutationFn: (email: string) => api.post('/api/auth/resend-verification', { email }) })
}

export function useForgotPassword() {
  return useMutation({ mutationFn: (email: string) => api.post('/api/auth/forgot-password', { email }) })
}

export function useResetPassword() {
  return useMutation({
    mutationFn: (input: { userId: string; token: string; password: string }) => api.post('/api/auth/reset-password', input),
  })
}

/* Organizations, members and invites. */

const orgKey = (orgId: string) => ['org', orgId] as const

export function useOrg(orgId: string | undefined) {
  return useQuery<OrgDetail>({
    queryKey: orgKey(orgId ?? ''),
    queryFn: () => api.get<OrgDetail>(`/api/orgs/${orgId}`),
    enabled: Boolean(orgId),
  })
}

export function useCreateOrg() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (name: string) => api.post<OrgSummary>('/api/orgs', { name }),
    onSuccess: () => client.invalidateQueries({ queryKey: ME_KEY }),
  })
}

export function useRenameOrg(orgId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (name: string) => api.patch<OrgSummary>(`/api/orgs/${orgId}`, { name }),
    onSuccess: () => Promise.all([client.invalidateQueries({ queryKey: ME_KEY }), client.invalidateQueries({ queryKey: orgKey(orgId) })]),
  })
}

export function useDeleteOrg(orgId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: () => api.delete(`/api/orgs/${orgId}`),
    onSuccess: async () => {
      client.removeQueries({ queryKey: orgKey(orgId) })
      await client.invalidateQueries({ queryKey: ME_KEY })
    },
  })
}

export function useMembers(orgId: string) {
  return useQuery<Member[]>({ queryKey: [...orgKey(orgId), 'members'], queryFn: () => api.get<Member[]>(`/api/orgs/${orgId}/members`) })
}

export function useChangeRole(orgId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ userId, role }: { userId: string; role: Role }) => api.patch<Member>(`/api/orgs/${orgId}/members/${userId}`, { role }),
    onSuccess: () => Promise.all([client.invalidateQueries({ queryKey: [...orgKey(orgId), 'members'] }), client.invalidateQueries({ queryKey: ME_KEY })]),
  })
}

export function useRemoveMember(orgId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (userId: string) => api.delete(`/api/orgs/${orgId}/members/${userId}`),
    onSuccess: () => Promise.all([client.invalidateQueries({ queryKey: [...orgKey(orgId), 'members'] }), client.invalidateQueries({ queryKey: ME_KEY })]),
  })
}

export function useInvites(orgId: string, enabled: boolean) {
  return useQuery<Invite[]>({ queryKey: [...orgKey(orgId), 'invites'], queryFn: () => api.get<Invite[]>(`/api/orgs/${orgId}/invites`), enabled })
}

export function useCreateInvite(orgId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (input: { email: string; role: Role }) => api.post<Invite>(`/api/orgs/${orgId}/invites`, input),
    onSuccess: () => client.invalidateQueries({ queryKey: [...orgKey(orgId), 'invites'] }),
  })
}

export function useRevokeInvite(orgId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (inviteId: string) => api.delete(`/api/orgs/${orgId}/invites/${inviteId}`),
    onSuccess: () => client.invalidateQueries({ queryKey: [...orgKey(orgId), 'invites'] }),
  })
}

export function useInvitePreview(token: string) {
  return useQuery<InvitePreview>({ queryKey: ['invite', token], queryFn: () => api.get<InvitePreview>(`/api/invites/${token}`), retry: false })
}

export function useAcceptInvite() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (token: string) => api.post<OrgSummary>(`/api/invites/${token}/accept`),
    onSuccess: () => client.invalidateQueries({ queryKey: ME_KEY }),
  })
}
