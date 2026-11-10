import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../lib/api-client'
import { queryKeys } from '../../lib/query-keys'

export interface NameplateOption { id: string; name: string; restricted: boolean; available: boolean; granted: boolean }
export interface NameplateState { selected: string | null; isAdmin: boolean; options: NameplateOption[] }

export function useNameplate() {
  return useQuery({ queryKey: queryKeys.nameplates.mine, queryFn: () => api.get<NameplateState>('/profile/nameplate'),
    staleTime: 0, refetchInterval: 60_000 })
}
export function useSelectNameplate() {
  const client = useQueryClient()
  return useMutation({ mutationFn: (id: string | null) => api.put<NameplateState>('/profile/nameplate', { id }),
    onSuccess: data => client.setQueryData(queryKeys.nameplates.mine, data),
    onError: () => { void client.invalidateQueries({ queryKey: queryKeys.nameplates.mine }) },
  })
}
export function useUserNameplate(userId: string) {
  return useQuery({ queryKey: queryKeys.nameplates.user(userId),
    queryFn: () => api.get<NameplateState>(`/admin/nameplates/${userId}`) })
}
export function useNameplateGrant(userId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ id, granted }: { id: string; granted: boolean }) => api.put<NameplateState>(`/admin/nameplates/${userId}/${id}`, { granted }),
    onSuccess: data => {
      client.setQueryData(queryKeys.nameplates.user(userId), data)
      void client.invalidateQueries({ queryKey: queryKeys.nameplates.mine })
    },
  })
}
