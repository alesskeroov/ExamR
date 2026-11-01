import { useQuery } from '@tanstack/react-query'
import { api, ApiError } from '../../lib/api-client'
import { queryKeys } from '../../lib/query-keys'

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

export function useHealth() {
  return useQuery({ queryKey: queryKeys.health, queryFn: getHealth, staleTime: 10_000 })
}
