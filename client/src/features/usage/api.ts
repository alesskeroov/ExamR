import { useEffect } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../lib/api-client'
import { queryKeys } from '../../lib/query-keys'
import type { PagedResult } from '../documents/api'

export type UsageKind = 'Upload' | 'Exam' | 'Battle' | 'AiRequest'
export interface UsageItem { kind: UsageKind; used: number; limit: number | null }
export interface Usage { resetsAt: string; unlimited: boolean; items: UsageItem[] }
export interface UsageLimits { monthlyUploads: number | null; monthlyExams: number | null; monthlyBattles: number | null; monthlyAiRequests: number | null; unlimited: boolean }
export interface AdminUsage { id: string; nickname: string; email: string; isAdmin: boolean; usage: Usage; custom: UsageLimits | null; defaults: UsageLimits }

export function useUsage() {
  const client = useQueryClient()
  const query = useQuery({ queryKey: queryKeys.usage.mine, queryFn: () => api.get<Usage>('/usage'), staleTime: 0, refetchOnWindowFocus: true })
  useEffect(() => {
    if (!query.data) return
    const delay = new Date(query.data.resetsAt).getTime() - Date.now()
    const timer = setTimeout(() => void client.invalidateQueries({ queryKey: queryKeys.usage.mine }), Math.min(Math.max(delay + 100, 1000), 2147483647))
    return () => clearTimeout(timer)
  }, [client, query.data])
  return query
}

export function useUsageLimit(kind: UsageKind) {
  const query = useUsage()
  const item = query.data?.items.find(x => x.kind === kind)
  return { ...query, item, blocked: !!item && item.limit !== null && item.used >= item.limit }
}

export function useAdminUsage(q: string, page: number) {
  return useQuery({ queryKey: queryKeys.usage.adminUsers(q, page), queryFn: () => api.get<PagedResult<AdminUsage>>(`/admin/usage/users?q=${encodeURIComponent(q)}&page=${page}`) })
}
export function useAdminUserUsage(id: string) {
  return useQuery({ queryKey: queryKeys.usage.adminUser(id), queryFn: () => api.get<AdminUsage>(`/admin/usage/users/${id}`), enabled: !!id })
}
export function useUpdateUsage() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ id, limits }: { id: string; limits: UsageLimits | null }) => limits === null ? api.delete(`/admin/usage/users/${id}`) : api.put(`/admin/usage/users/${id}`, limits),
    onSuccess: () => client.invalidateQueries({ queryKey: queryKeys.usage.all }),
  })
}
