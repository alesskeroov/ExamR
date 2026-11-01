import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../lib/api-client'
import { queryKeys } from '../../lib/query-keys'

export interface AdminUser { id: string; nickname: string; email: string; avatarUrl: string | null; isAdmin: boolean }

export function useAdmins() {
  return useQuery({ queryKey: queryKeys.admins.list, queryFn: () => api.get<AdminUser[]>('/admin/admins') })
}
export function useAdminUserSearch(term: string) {
  return useQuery({ queryKey: queryKeys.admins.search(term), enabled: term.length >= 2,
    queryFn: () => api.get<AdminUser[]>(`/admin/admins/users?q=${encodeURIComponent(term)}`) })
}
export function useAdminRole() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ userId, grant }: { userId: string; grant: boolean }) =>
      grant ? api.post(`/admin/admins/${userId}`, {}) : api.delete(`/admin/admins/${userId}`),
    onSuccess: () => client.invalidateQueries({ queryKey: queryKeys.admins.all }),
  })
}
