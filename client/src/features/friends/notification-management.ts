import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api } from '../../lib/api-client'
import { queryKeys } from '../../lib/query-keys'

export function useDeleteNotifications() {
  const client = useQueryClient()
  return useMutation({ mutationFn: ({ id, readOnly }: { id?: string; readOnly?: boolean }) => api.delete(id ? `/notifications/${id}` : `/notifications?readOnly=${readOnly === true}`),
    onSuccess: () => client.invalidateQueries({ queryKey: queryKeys.social.all }) })
}
