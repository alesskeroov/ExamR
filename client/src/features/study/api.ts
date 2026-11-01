import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../lib/api-client'
import { queryKeys } from '../../lib/query-keys'

export type StudyMode = 'open' | 'mcq'
export interface StudyDocument { id: string; originalFileName: string; questionCount: number }
export interface PracticeFeedback {
  questionId: string; topic: string; correct: boolean; explanation: string; selectedIndex: number; correctIndex: number
}
export interface PracticeResult {
  total: number; correct: number; incorrect: number; percent: number; weakTopics: string[]; mistakes: PracticeFeedback[]
}
export interface PracticeView {
  id: string; mode: StudyMode; position: number; total: number
  question: { id: string; number: number; text: string; options: string[] } | null
  evaluation?: { score: number; verdict: string; missingConcepts: string[] } | null; answer?: string | null; feedback: PracticeFeedback | null; help: string | null; finished: boolean; result: PracticeResult | null
}
export interface PracticeSummary {
  id: string; mode: StudyMode; createdAt: string; finished: boolean
  answered: number; total: number; result: PracticeResult | null
}
type PracticeRequest =
  | { kind: 'start'; mode: StudyMode; count: number }
  | { kind: 'load'; id: string }
  | { kind: 'action'; id: string; questionId: string; action: 'next' | 'hint' | 'explain' | 'reveal' | 'answer' | 'respond'; optionIndex?: number; answer?: string }

export function useStudyDocuments() {
  return useQuery({ queryKey: queryKeys.study.documents, queryFn: () => api.get<StudyDocument[]>('/study/documents') })
}
export function usePracticeHistory(documentId: string) {
  return useQuery({
    queryKey: queryKeys.study.history(documentId),
    queryFn: () => api.get<PracticeSummary[]>(`/study/${documentId}/practice/history`),
    enabled: !!documentId,
  })
}
export function usePractice(documentId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (request: PracticeRequest) => {
      if (request.kind === 'load') return api.get<PracticeView>(`/study/practice/${request.id}`)
      if (request.kind === 'start') return api.post<PracticeView>('/study/practice', {
        documentId, mode: request.mode, count: request.count,
      })
      return api.post<PracticeView>(`/study/practice/${request.id}/action`, {
        action: request.action, questionId: request.questionId, optionIndex: request.optionIndex, answer: request.answer,
      })
    },
    onSettled: () => {
      void client.invalidateQueries({ queryKey: queryKeys.study.history(documentId) })
      void client.invalidateQueries({ queryKey: queryKeys.study.progress(documentId) })
    },
  })
}

export function useDeletePractice(documentId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.delete<void>(`/study/practice/${id}`),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: queryKeys.study.history(documentId) })
      void client.invalidateQueries({ queryKey: queryKeys.study.progress(documentId) })
    },
  })
}
