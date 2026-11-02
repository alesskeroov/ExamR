import { keepPreviousData, useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, uploadFile } from '../../lib/api-client'
import { queryKeys } from '../../lib/query-keys'

export type DocumentStatus = 'Pending' | 'Processing' | 'Completed' | 'Failed'
export type VerificationStatus = 'Unverified' | 'AutoDetected' | 'Verified'

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
}

export interface ProcessingReport {
  questionsFound: number
  autoDetected: number
  unverified: number
  duplicatesSkipped: number
  warnings: string[]
}

export interface DocumentDto {
  id: string
  originalFileName: string
  fileSizeBytes: number
  pageCount: number | null
  status: DocumentStatus
  uploadedBy: { id: string; displayName: string }
  sharingStatus: 'Private' | 'Users' | 'Groups' | 'UsersAndGroups' | 'Everyone'
  canEdit: boolean
  questionCount: number
  processingReport: ProcessingReport | null
  errorMessage: string | null
  createdAt: string
  processedAt: string | null
}

export interface AnswerOptionDto {
  id: string
  label: string
  text: string
  isCorrect: boolean
}

export interface QuestionDto {
  id: string
  documentId: string
  sourceNumber: number
  text: string
  verificationStatus: VerificationStatus
  parseWarning: string | null
  options: AnswerOptionDto[]
  documentName: string
  answer: string | null
  isBookmarked: boolean
  canEdit: boolean
}

export type QuestionFilter = 'all' | 'answered' | 'unverified' | 'warnings'

export const MAX_FILE_SIZE_BYTES = 20 * 1024 * 1024
const QUESTIONS_PAGE_SIZE = 20
const isProcessing = (status?: DocumentStatus) => status === 'Pending' || status === 'Processing'

export function useMyDocuments(page = 1) {
  return useQuery({
    queryKey: queryKeys.documents.minePage(page),
    queryFn: () => api.get<PagedResult<DocumentDto>>(`/documents?mine=true&page=${page}&pageSize=10`),
    // Keep the list fresh while something in it is still being processed.
    refetchInterval: (query) => (query.state.data?.items.some((d) => isProcessing(d.status)) ? 2000 : false),
  })
}

/** The current user's processed multiple-choice exam sources. */
export function useCompletedDocuments(forBattle = false) {
  return useQuery({
    queryKey: forBattle ? queryKeys.documents.battle : queryKeys.documents.completed,
    queryFn: () => api.get<PagedResult<DocumentDto>>(`/documents?status=Completed&forExam=true&pageSize=100&${forBattle ? 'forBattle=true' : 'includeShared=true'}`),
  })
}

/** Polls every 1.5 s until processing finishes. */
export function useDocument(id: string | null) {
  return useQuery({
    queryKey: queryKeys.documents.detail(id ?? ''),
    queryFn: () => api.get<DocumentDto>(`/documents/${id}`),
    enabled: id !== null,
    refetchInterval: (query) => (isProcessing(query.state.data?.status) ? 1500 : false),
  })
}

export function useUploadDocument(onProgress: (percent: number) => void) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (file: File) => uploadFile<DocumentDto>('/documents', file, onProgress),
    onSuccess: (document) => {
      queryClient.setQueryData(queryKeys.documents.detail(document.id), document)
      void queryClient.invalidateQueries({ queryKey: queryKeys.documents.completed })
      void queryClient.invalidateQueries({ queryKey: queryKeys.study.documents })
      void queryClient.invalidateQueries({ queryKey: queryKeys.documents.mine })
    },
  })
}

export function useDeleteDocument() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.delete(`/documents/${id}`),
    onSuccess: (_, id) => {
      queryClient.removeQueries({ queryKey: queryKeys.documents.detail(id) })
      queryClient.removeQueries({ queryKey: queryKeys.questions.document(id) })
      void queryClient.invalidateQueries({ queryKey: queryKeys.documents.mine })
      void queryClient.invalidateQueries({ queryKey: queryKeys.documents.completed })
      void queryClient.invalidateQueries({ queryKey: queryKeys.study.documents })
    },
  })
}

export function useReprocessDocument() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.post<DocumentDto>(`/documents/${id}/reprocess`),
    onSuccess: (document) => {
      queryClient.setQueryData(queryKeys.documents.detail(document.id), document)
      void queryClient.invalidateQueries({ queryKey: ['questions'] })
      void queryClient.invalidateQueries({ queryKey: queryKeys.documents.completed })
      void queryClient.invalidateQueries({ queryKey: queryKeys.study.documents })
      void queryClient.invalidateQueries({ queryKey: queryKeys.documents.mine })
    },
  })
}

const filterParams: Record<QuestionFilter, string> = {
  all: '',
  answered: '&status=AutoDetected',
  unverified: '&status=Unverified',
  warnings: '&hasWarning=true',
}

export function useDocumentQuestions(documentId: string, filter: QuestionFilter, enabled: boolean) {
  return useInfiniteQuery({
    queryKey: queryKeys.questions.byDocument(documentId, filter),
    queryFn: ({ pageParam }) =>
      api.get<PagedResult<QuestionDto>>(
        `/questions?documentId=${documentId}&page=${pageParam}&pageSize=${QUESTIONS_PAGE_SIZE}${filterParams[filter]}`,
      ),
    initialPageParam: 1,
    getNextPageParam: (last) => (last.page * last.pageSize < last.totalCount ? last.page + 1 : undefined),
    enabled,
    placeholderData: keepPreviousData,
  })
}

export function useSharedDocuments(page = 1) {
  return useQuery({ queryKey: queryKeys.documents.shared(page),
    queryFn: () => api.get<PagedResult<DocumentDto>>(`/documents?shared=true&page=${page}&pageSize=10`), refetchInterval: 15_000 })
}
