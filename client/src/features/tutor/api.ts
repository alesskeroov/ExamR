import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, uploadFile } from '../../lib/api-client'
import { queryKeys } from '../../lib/query-keys'
import type { PagedResult } from '../documents/api'

export type TutorMode = 'Free' | 'StepByStep'
export type IndexStatus = 'Pending' | 'Processing' | 'Completed' | 'Failed'

export interface TutorDocument {
  documentId: string; fileName: string; format: string; status: IndexStatus | null; pageCount: number | null; chunkCount: number; uploadedAt: string
}
export interface IndexedDocument {
  documentId: string; fileName: string; format: string; status: IndexStatus; pageCount: number | null; chunkCount: number; error: string | null
}
export interface ChatSession {
  id: string; documentId: string; documentName: string; title: string; createdAt: string; updatedAt: string; messageCount: number
}
export interface ChatSource {
  number: number; chunkOrdinal: number; pageFrom: number | null; pageTo: number | null; section: string | null; excerpt: string; cited: boolean
}
export interface ChatMessage {
  id: string; role: 'User' | 'Assistant'; content: string; status: 'Completed' | 'Cancelled' | 'Failed'; grounded: boolean | null; createdAt: string; sources: ChatSource[]
}
export interface ChatDetail { session: ChatSession; messages: ChatMessage[] }
export interface ChunkItem { ordinal: number; text: string; pageFrom: number | null; pageTo: number | null; section: string | null }

export function useTutorDocuments() {
  return useQuery({
    queryKey: queryKeys.tutor.documents,
    queryFn: () => api.get<TutorDocument[]>('/learning/documents'),
    // Keep polling while something is being indexed so the picker updates by itself.
    refetchInterval: (query) => query.state.data?.some(d => d.status === 'Pending' || d.status === 'Processing') ? 2500 : false,
  })
}

export function useChats() {
  return useQuery({ queryKey: queryKeys.tutor.chats, queryFn: () => api.get<PagedResult<ChatSession>>('/learning/chats?pageSize=100') })
}

export function useChat(id: string | null) {
  return useQuery({ queryKey: queryKeys.tutor.chat(id ?? ''), queryFn: () => api.get<ChatDetail>(`/learning/chats/${id}`), enabled: !!id })
}

export function useDocumentChunks(documentId: string | null, enabled: boolean) {
  return useQuery({
    queryKey: queryKeys.tutor.chunks(documentId ?? ''),
    queryFn: () => api.get<PagedResult<ChunkItem>>(`/learning/documents/${documentId}/chunks?pageSize=100`),
    enabled: !!documentId && enabled,
    staleTime: 5 * 60_000,
  })
}

export function useCreateChat() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (documentId: string) => api.post<ChatSession>('/learning/chats', { documentId }),
    onSuccess: () => client.invalidateQueries({ queryKey: queryKeys.tutor.chats }),
  })
}

export function useDeleteChat() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.delete(`/learning/chats/${id}`),
    onSuccess: (_, id) => {
      client.removeQueries({ queryKey: queryKeys.tutor.chat(id) })
      return client.invalidateQueries({ queryKey: queryKeys.tutor.chats })
    },
  })
}

/** Uploads a file for the tutor, or starts indexing an existing document. */
export function useIndexDocument() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (input: { file: File; onProgress?: (percent: number) => void } | { documentId: string }) =>
      'file' in input
        ? uploadFile<IndexedDocument>('/learning/documents', input.file, input.onProgress)
        : api.post<IndexedDocument>(`/learning/documents/${input.documentId}/process`),
    onSuccess: () => client.invalidateQueries({ queryKey: queryKeys.tutor.documents }),
  })
}

/** Non-admins receive only `enabled`; budget and usage figures are admin-only. */
export interface AiUsage {
  enabled: boolean
  model?: string; totalBudgetUsd?: number; spentUsd?: number; reservedUsd?: number; remainingUsd?: number
  requestsToday?: number; dailyRequestsPerUser?: number
}

export function useAiUsage() {
  return useQuery({ queryKey: queryKeys.tutor.usage, queryFn: () => api.get<AiUsage>('/learning/ai/usage'), staleTime: 30_000 })
}

export type QuizStatus = 'Correct' | 'Partial' | 'Incorrect'
export interface QuizQuestion { id: string; question: string; pageFrom: number | null; pageTo: number | null }
export interface QuizResult {
  id: string; question: string; status: QuizStatus; feedback: string; correctAnswer: string; missingPoints: string[]
  pageFrom: number | null; pageTo: number | null; aiChecked: boolean
}
export interface QuizReview { correct: number; partial: number; incorrect: number; items: QuizResult[] }
export interface OpenQuestion { number: number; text: string; chunkOrdinal: number; page: number | null; hasAnswer: boolean }
export interface OpenQuestions { supported: boolean; warning: string | null; questions: OpenQuestion[] }
export type AnswerLength = 'Short' | 'Long'
export interface SharedQuestion { number: number; text: string; addition: string; needsLonger: boolean; supported: boolean; hasDocumentAnswer: boolean }
export interface SharedGroup { id: number; title: string; answer: string; keywords: string[]; sourcePages: number[]; questions: SharedQuestion[] }
export interface SharedBase { title: string; answer: string; keyPoints: string[]; topicIds: number[]; sourcePages: number[] }
export interface SharedPayload { auto: boolean; hasAnswers: boolean; base: SharedBase | null; groups: SharedGroup[] }
export interface LastMinuteSheet {
  topics: { title: string; explanation: string; remember: string[]; related: string[] }[]; keyTerms: string[]
  sourcePages: number[]; readingMinutes: number; fromQuestions: boolean
}
export interface Material<T = unknown> {
  id: string; kind: 'SharedAnswer' | 'LastMinute'; length: AnswerLength | null; minutes: number | null; saved: boolean; fromCache: boolean; createdAt: string; payload: T
}
export interface MaterialSummary { id: string; kind: 'SharedAnswer' | 'LastMinute'; length: AnswerLength | null; minutes: number | null; saved: boolean; title: string; items: number; createdAt: string }

export function useQuizReview(chatId: string) {
  return useQuery({ queryKey: queryKeys.tutor.review(chatId), queryFn: () => api.get<QuizReview>(`/learning/chats/${chatId}/quiz/review`) })
}
export function useNextQuestion(chatId: string) {
  return useMutation({ mutationFn: () => api.post<QuizQuestion>(`/learning/chats/${chatId}/quiz`) })
}
export function useAnswerQuestion(chatId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ id, answer, giveUp }: { id: string; answer?: string; giveUp?: boolean }) => api.post<QuizResult>(`/learning/quiz/${id}/answer`, { answer, giveUp }),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: queryKeys.tutor.review(chatId) })
      void client.invalidateQueries({ queryKey: queryKeys.tutor.usage })
    },
  })
}
export function useOpenQuestions(chatId: string) {
  return useQuery({ queryKey: queryKeys.tutor.openQuestions(chatId), queryFn: () => api.get<OpenQuestions>(`/learning/chats/${chatId}/open-questions`), staleTime: 5 * 60_000 })
}
export function useMaterials(chatId: string) {
  return useQuery({ queryKey: queryKeys.tutor.materials(chatId), queryFn: () => api.get<MaterialSummary[]>(`/learning/chats/${chatId}/materials`) })
}
export function useMaterial<T>(id: string | null) {
  return useQuery({ queryKey: queryKeys.tutor.material(id ?? ''), queryFn: () => api.get<Material<T>>(`/learning/materials/${id}`), enabled: !!id, staleTime: Infinity })
}
function useMaterialMutation<TBody, TPayload>(chatId: string, path: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (body: TBody) => api.post<Material<TPayload>>(`/learning/chats/${chatId}/${path}`, body),
    onSuccess: material => {
      client.setQueryData(queryKeys.tutor.material(material.id), material)
      void client.invalidateQueries({ queryKey: queryKeys.tutor.materials(chatId) })
      void client.invalidateQueries({ queryKey: queryKeys.tutor.usage })
    },
  })
}
export const useSharedAnswers = (chatId: string) =>
  useMaterialMutation<{ auto: boolean; questionNumbers?: number[]; length: AnswerLength }, SharedPayload>(chatId, 'shared-answers')
export const useLastMinute = (chatId: string) => useMaterialMutation<{ minutes: number }, LastMinuteSheet>(chatId, 'last-minute')
export function useSaveMaterial(chatId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ id, saved }: { id: string; saved: boolean }) => api.post<void>(`/learning/materials/${id}/save`, { saved }),
    onSuccess: (_, { id, saved }) => {
      client.setQueryData<Material>(queryKeys.tutor.material(id), m => m && { ...m, saved })
      void client.invalidateQueries({ queryKey: queryKeys.tutor.materials(chatId) })
    },
  })
}
export function useDeleteMaterial(chatId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.delete(`/learning/materials/${id}`),
    onSuccess: () => client.invalidateQueries({ queryKey: queryKeys.tutor.materials(chatId) }),
  })
}
