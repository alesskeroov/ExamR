import { useQuery } from '@tanstack/react-query'
import { api, ApiError } from '../../lib/api-client'
import { queryKeys } from '../../lib/query-keys'
import type { PagedResult } from '../documents/api'
import type { ExamSummary } from '../exams/api'

export interface HealthResponse {
  status: 'Healthy' | 'Degraded'
  databaseConnected: boolean
  serverTime: string
}

async function getHealth(): Promise<HealthResponse> {
  try {
    return await api.get<HealthResponse>('/health')
  } catch (error) {
    // 503 still carries a health body: the API is up but the database is not.
    if (error instanceof ApiError && error.status === 503 && error.problem) {
      return error.problem as unknown as HealthResponse
    }
    throw error
  }
}

// Fetches the whole exam history (100 per page) so dashboard totals are exact.
export function useAllExams(enabled = true) {
  return useQuery({ queryKey: queryKeys.exams.allHistory, enabled, staleTime: 30_000, queryFn: async () => {
    const items: ExamSummary[] = []
    for (let page = 1; ; page++) {
      const result = await api.get<PagedResult<ExamSummary>>(`/exams?page=${page}&pageSize=100`)
      items.push(...result.items)
      if (items.length >= result.totalCount || result.items.length === 0) return items
    }
  } })
}

export function useHealth() {
  return useQuery({ queryKey: queryKeys.health, queryFn: getHealth, staleTime: 10_000 })
}
