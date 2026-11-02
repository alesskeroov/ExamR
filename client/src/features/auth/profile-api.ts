import { useQuery } from '@tanstack/react-query'
import { api } from '../../lib/api-client'
import { queryKeys } from '../../lib/query-keys'
import type { BattleHistoryItem } from '../battle/api'
import type { PagedResult } from '../documents/api'

export function useProfileBattles(enabled = true) {
  return useQuery({ queryKey: queryKeys.battles.allHistory, enabled, queryFn: async () => {
    const items: BattleHistoryItem[] = []
    for (let page = 1; ; page++) {
      const result = await api.get<PagedResult<BattleHistoryItem>>(`/battles/history?page=${page}&pageSize=100`)
      items.push(...result.items)
      if (items.length >= result.totalCount || !result.items.length) return items
    }
  }, staleTime: 30_000 })
}
