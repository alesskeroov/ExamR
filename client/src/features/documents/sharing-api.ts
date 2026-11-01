import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, apiRequest } from '../../lib/api-client'
import { queryKeys } from '../../lib/query-keys'

export interface SharingUser { id: string; nickname: string }
export interface SharingSettings { shareWithEveryone: boolean; users: SharingUser[]; groupIds: string[] }
export function useSharing(id: string) {
  return useQuery({ queryKey: queryKeys.sharing.detail(id), queryFn: () => api.get<SharingSettings>(`/admin/documents/${id}/sharing`) })
}
export function useSharingUsers(term: string) {
  return useQuery({ queryKey: queryKeys.sharing.users(term), enabled: term.trim().length >= 2,
    queryFn: () => api.get<SharingUser[]>(`/admin/documents/users?q=${encodeURIComponent(term.trim())}`) })
}
export function useSaveSharing(id: string) {
  const client = useQueryClient()
  return useMutation({ mutationFn: (body: { shareWithEveryone: boolean; userIds: string[]; groupIds: string[] }) =>
    apiRequest(`/admin/documents/${id}/sharing`, { method: 'PUT', body }),
    onSuccess: () => Promise.all([
      client.invalidateQueries({ queryKey: queryKeys.documents.all }),
      client.invalidateQueries({ queryKey: queryKeys.sharing.all }),
      client.invalidateQueries({ queryKey: queryKeys.study.documents }),
    ]) })
}
