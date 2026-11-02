import { useQuery } from '@tanstack/react-query'
import { api } from '../../lib/api-client'
import { queryKeys } from '../../lib/query-keys'

export interface BattleRank {
  name: string; rating: number; highestRating: number; totalRankedBattles: number
  wins: number; losses: number; draws: number; nextRank: string | null; remaining: number; progress: number; lastRatingUpdate: string | null
}
export interface RankLeader { userId: string; nickname: string; avatarUrl: string | null; rank: BattleRank }
export function useBattleRank() {
  return useQuery({ queryKey: queryKeys.battles.rank, queryFn: () => api.get<BattleRank>('/battle-rank/me'), staleTime: 30_000 })
}
export function useRankLeaderboard() {
  return useQuery({ queryKey: queryKeys.battles.leaderboard, queryFn: () => api.get<RankLeader[]>('/battle-rank/leaderboard'), staleTime: 30_000 })
}

export function usePlayerRanks(userIds: string[]) {
  const ids = [...new Set(userIds)].sort().join(',')
  return useQuery({ queryKey: queryKeys.battles.playerRanks(ids), enabled: !!ids,
    queryFn: () => api.get<{ userId: string; rank: BattleRank }[]>(`/battle-rank/players?ids=${encodeURIComponent(ids)}`), staleTime: 30_000 })
}
