import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, apiRequest, ApiError } from '../../lib/api-client'
import { queryKeys } from '../../lib/query-keys'
import { useCurrentUser } from '../auth/api'

export interface StudyGroup { id: string; name: string; description: string; createdAt: string; memberCount: number }
export interface GroupMember { id: string; nickname: string; avatarUrl: string | null }
export interface GroupDocument { id: string; fileName: string; questionCount: number }
export interface GroupSearchUser extends GroupMember { isMember: boolean }

async function groupList(admin: boolean) {
  try { return await api.get<StudyGroup[]>(admin ? '/admin/groups' : '/groups') }
  catch (error) {
    if (error instanceof ApiError && error.status === 404 && !error.problem?.detail)
      throw new Error('İşləyən API qrup modulunu tanımır. Yeni backend və qrup migration-u tətbiq edilməlidir.')
    throw error
  }
}
export function useGroups(admin = false) {
  const { data: user } = useCurrentUser()
  return useQuery({ queryKey: queryKeys.groups.list(user?.id ?? '', admin), enabled: !!user && (!admin || user.roles.includes('Admin')),
    queryFn: () => groupList(admin), refetchInterval: 30_000 })
}
export function useGroup(id: string) {
  const { data: user } = useCurrentUser()
  return useQuery({ queryKey: queryKeys.groups.detail(user?.id ?? '', id), enabled: !!user && !!id,
    queryFn: () => api.get<StudyGroup>(`/groups/${id}`), refetchInterval: 15_000 })
}
export function useGroupMembers(id: string) {
  const { data: user } = useCurrentUser()
  return useQuery({ queryKey: queryKeys.groups.members(user?.id ?? '', id), enabled: !!user && !!id,
    queryFn: () => api.get<GroupMember[]>(`/groups/${id}/members`), refetchInterval: 15_000 })
}
export function useGroupDocuments(id: string) {
  const { data: user } = useCurrentUser()
  return useQuery({ queryKey: queryKeys.groups.documents(user?.id ?? '', id), enabled: !!user && !!id,
    queryFn: () => api.get<GroupDocument[]>(`/groups/${id}/documents`) })
}
export function useGroupUserSearch(id: string, term: string) {
  const { data: user } = useCurrentUser()
  return useQuery({ queryKey: queryKeys.groups.search(user?.id ?? '', id, term), enabled: !!id && term.length >= 2 && !!user?.roles.includes('Admin'),
    queryFn: () => api.get<GroupSearchUser[]>(`/admin/groups/${id}/users?q=${encodeURIComponent(term)}`) })
}
type GroupAction =
  | { kind: 'save'; id?: string; name: string; description: string }
  | { kind: 'delete'; id: string }
  | { kind: 'add'; id: string; userIds: string[] }
  | { kind: 'remove'; id: string; userId: string }
export function useGroupAction() {
  const client = useQueryClient()
  return useMutation({ mutationFn: async (action: GroupAction) => {
    if (action.kind === 'save') {
      const body = { name: action.name, description: action.description }
      if (action.id) await apiRequest(`/admin/groups/${action.id}`, { method: 'PUT', body })
      else await api.post('/admin/groups', body)
    } else if (action.kind === 'delete') await api.delete(`/admin/groups/${action.id}`)
    else if (action.kind === 'add') await api.post(`/admin/groups/${action.id}/members`, { userIds: action.userIds })
    else await api.delete(`/admin/groups/${action.id}/members/${action.userId}`)
  }, onSuccess: () => client.invalidateQueries({ queryKey: queryKeys.groups.all }) })
}
