import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../lib/api-client'
import { queryKeys } from '../../lib/query-keys'
import type { PagedResult, QuestionDto } from '../documents/api'

export type FocusActivity = 'exam' | 'mistakes' | 'study'
export interface FocusStats { todaySeconds: number; weekSeconds: number; completedSessions: number; lastSevenDays: { date: string; focusSeconds: number }[] }
export interface SaveFocusSession { activity: FocusActivity; plannedMinutes: number; focusSeconds: number; answered: number; correct: number; completed: boolean; startedAt: string }

export function useFocusStats() {
  return useQuery({ queryKey: queryKeys.focus.stats,
    queryFn: () => api.get<FocusStats>(`/focus/stats?offset=${new Date().getTimezoneOffset()}`) })
}

export function useSaveFocusSession() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (session: SaveFocusSession) => api.post<void>('/focus/sessions', session),
    onSuccess: () => client.invalidateQueries({ queryKey: queryKeys.focus.stats }),
  })
}

/** Multiple-choice questions the user answered incorrectly in past exams. */
export function useFocusQuestions(activity: FocusActivity) {
  return useQuery({ queryKey: queryKeys.focus.questions('wrong'), enabled: activity === 'mistakes', staleTime: Infinity,
    queryFn: () => api.get<PagedResult<QuestionDto>>('/questions?wrong=true&type=choice&pageSize=100') })
}
