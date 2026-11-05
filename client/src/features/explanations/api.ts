import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../lib/api-client'
import { queryKeys } from '../../lib/query-keys'

export type ExplanationStatus = 'Draft' | 'Review' | 'Approved' | 'Rejected'
export type ExplanationQuestionType = 'Theory' | 'Math'
export type ExplanationJobStatus = 'Pending' | 'Running' | 'Completed' | 'Failed' | 'Cancelled' | 'BudgetExhausted'
export type ExplanationFilter = 'all' | 'missing' | 'draft' | 'review' | 'approved' | 'rejected' | 'failed' | 'unexplainable'

export interface ExplanationAiStatus {
  enabled: boolean
  provider: 'Mock' | 'OpenAI'
  realCalls: boolean
  apiKeyConfigured: boolean
  theoryModel: string
  mathModel: string
  monthlyBudgetUsd: number
  monthSpentUsd: number
  remainingUsd: number
  maxQuestionsPerJob: number
}

export interface ExplanationDocument { id: string; name: string; questions: number }

export interface AdminExplanation {
  id: string
  status: ExplanationStatus
  questionType: ExplanationQuestionType
  explanation: string
  reviewReason: string | null
  model: string | null
  costUsd: number
  shared: boolean
  updatedAt: string
  rowVersion: string
}

export interface AdminExplanationQuestion {
  questionId: string
  sourceNumber: number
  text: string
  options: { label: string; text: string; isCorrect: boolean }[]
  explainable: boolean
  failedLastRun: boolean
  explanation: AdminExplanation | null
}

export interface ExplanationCounts { total: number; notExplainable: number; missing: number; draft: number; review: number; approved: number; rejected: number; failed: number }

export interface AdminExplanationPage { items: AdminExplanationQuestion[]; page: number; pageSize: number; totalCount: number; counts: ExplanationCounts }

export interface ExplanationEstimate {
  documentId: string
  pending: number
  estimate: { questions: number; theoryQuestions: number; mathQuestions: number; requests: number; inputTokens: number; outputTokens: number; estimatedUsd: number }
  remainingUsd: number
  withinBudget: boolean
  hasActiveJob: boolean
}

export interface ExplanationJob {
  id: string
  documentId: string
  status: ExplanationJobStatus
  provider: string
  total: number
  done: number
  failed: number
  requests: number
  inputTokens: number
  outputTokens: number
  estimatedUsd: number
  spentUsd: number
  lastError: string | null
  failedQuestionIds: string[]
  createdAt: string
  completedAt: string | null
}

export const isActiveJob = (job?: ExplanationJob) => job?.status === 'Pending' || job?.status === 'Running'

export function useExplanationStatus() {
  return useQuery({ queryKey: queryKeys.explanations.status, queryFn: () => api.get<ExplanationAiStatus>('/admin/explanations/status') })
}

export function useExplanationDocuments() {
  return useQuery({ queryKey: queryKeys.explanations.documents, queryFn: () => api.get<ExplanationDocument[]>('/admin/explanations/documents') })
}

export function useExplanationQuestions(documentId: string, filter: ExplanationFilter, search: string, page: number) {
  const params = new URLSearchParams({ filter, page: String(page), pageSize: '20' })
  if (search.trim()) params.set('search', search.trim())
  return useQuery({
    queryKey: queryKeys.explanations.questions(documentId, params.toString()),
    queryFn: () => api.get<AdminExplanationPage>(`/admin/explanations/documents/${documentId}/questions?${params}`),
    enabled: documentId !== '',
    placeholderData: keepPreviousData,
  })
}

export function useExplanationEstimate(documentId: string) {
  return useQuery({
    queryKey: queryKeys.explanations.estimate(documentId),
    queryFn: () => api.get<ExplanationEstimate>(`/admin/explanations/documents/${documentId}/estimate`),
    enabled: documentId !== '',
  })
}

/** Polls every 2 s while a run is active; when it ends, the lists and estimate are refreshed. */
export function useExplanationJobs(documentId: string) {
  return useQuery({
    queryKey: queryKeys.explanations.jobs(documentId),
    queryFn: () => api.get<ExplanationJob[]>(`/admin/explanations/documents/${documentId}/jobs`),
    enabled: documentId !== '',
    refetchInterval: query => (isActiveJob(query.state.data?.[0]) ? 2000 : false),
  })
}

function useInvalidateDocument() {
  const client = useQueryClient()
  return (documentId: string) => Promise.all([
    client.invalidateQueries({ queryKey: queryKeys.explanations.document(documentId) }),
    client.invalidateQueries({ queryKey: queryKeys.explanations.status }),
  ])
}

export function useStartExplanationJob(documentId: string) {
  const invalidate = useInvalidateDocument()
  return useMutation({
    mutationFn: (confirmedEstimateUsd: number) => api.post<ExplanationJob>(`/admin/explanations/documents/${documentId}/jobs`, { confirmedEstimateUsd }),
    onSettled: () => invalidate(documentId),
  })
}

export function useCancelExplanationJob(documentId: string) {
  const invalidate = useInvalidateDocument()
  return useMutation({
    mutationFn: (jobId: string) => api.post<ExplanationJob>(`/admin/explanations/jobs/${jobId}/cancel`),
    onSettled: () => invalidate(documentId),
  })
}

export interface SaveExplanation { questionId: string; explanation: string; questionType: ExplanationQuestionType; rowVersion: string | null; approve: boolean }

export function useSaveExplanation(documentId: string) {
  const invalidate = useInvalidateDocument()
  return useMutation({
    mutationFn: ({ questionId, ...body }: SaveExplanation) => api.put<AdminExplanation>(`/admin/explanations/questions/${questionId}`, body),
    onSuccess: () => invalidate(documentId),
  })
}

export type ExplanationAction = { kind: 'approve' | 'reject' | 'delete'; id: string } | { kind: 'approve-many'; ids: string[] }

export function useExplanationAction(documentId: string) {
  const invalidate = useInvalidateDocument()
  return useMutation({
    mutationFn: (action: ExplanationAction) =>
      action.kind === 'approve-many' ? api.post<{ approved: number; skipped: number }>('/admin/explanations/approve', { ids: action.ids })
        : action.kind === 'delete' ? api.delete(`/admin/explanations/${action.id}`)
          : api.post(`/admin/explanations/${action.id}/${action.kind}`),
    onSuccess: () => invalidate(documentId),
  })
}
