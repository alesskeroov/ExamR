import { useEffect } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../lib/api-client'
import { queryKeys } from '../../lib/query-keys'

export type UsageKind = 'Upload' | 'Exam' | 'Battle' | 'AiRequest' | 'DimTest' | 'Guide'
export interface UsageItem { kind: UsageKind; used: number; limit: number | null }
export interface Usage { resetsAt: string; unlimited: boolean; items: UsageItem[] }

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
