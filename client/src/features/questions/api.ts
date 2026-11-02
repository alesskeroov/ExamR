import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, apiRequest } from '../../lib/api-client'
import { queryKeys } from '../../lib/query-keys'
import type { DocumentDto, PagedResult, QuestionDto } from '../documents/api'

export interface QuestionStats { totalQuestions: number; documentCount: number; bookmarkCount: number }
export interface BankFilters { search: string; documentId: string; type: '' | 'choice' | 'open'; status: '' | 'Verified' | 'AutoDetected' | 'Unverified'; bookmarked: boolean }
export interface QuestionEdit { text: string; answer: string | null; options: { id: string; text: string; isCorrect: boolean }[] }

export const BANK_PAGE_SIZE = 20

export function useQuestionBank(filters: BankFilters, page: number) {
  const params = new URLSearchParams({ page: String(page), pageSize: String(BANK_PAGE_SIZE) })
  if (filters.search.trim()) params.set('search', filters.search.trim())
  if (filters.documentId) params.set('documentId', filters.documentId)
  if (filters.type) params.set('type', filters.type)
  if (filters.status) params.set('status', filters.status)
  if (filters.bookmarked) params.set('bookmarked', 'true')
  const query = params.toString()
  return useQuery({ queryKey: queryKeys.questions.bank(query), placeholderData: keepPreviousData,
    queryFn: () => api.get<PagedResult<QuestionDto>>(`/questions?${query}`) })
}

export function useBankDocuments() {
  return useQuery({ queryKey: queryKeys.questions.documents,
    queryFn: () => api.get<PagedResult<DocumentDto>>('/documents?status=Completed&includeShared=true&pageSize=100') })
}

export function useQuestionStats() {
  return useQuery({ queryKey: queryKeys.questions.stats, queryFn: () => api.get<QuestionStats>('/questions/stats') })
}

type QuestionAction =
  | { kind: 'bookmark'; id: string; on: boolean }
  | { kind: 'verify'; id: string }
  | { kind: 'edit'; id: string; edit: QuestionEdit }

export function useQuestionAction() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: async (action: QuestionAction) => {
      if (action.kind === 'bookmark') await apiRequest(`/questions/${action.id}/bookmark`, { method: action.on ? 'PUT' : 'DELETE' })
      else if (action.kind === 'verify') await api.post(`/questions/${action.id}/verify`)
      else await apiRequest(`/questions/${action.id}`, { method: 'PUT', body: action.edit })
    },
    onSuccess: () => client.invalidateQueries({ queryKey: queryKeys.questions.all }),
  })
}
