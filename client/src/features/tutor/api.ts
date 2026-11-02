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

export interface AiUsage {
  model: string; totalBudgetUsd: number; spentUsd: number; reservedUsd: number; remainingUsd: number
  requestsToday: number; dailyRequestsPerUser: number; enabled: boolean
}

export function useAiUsage() {
  return useQuery({ queryKey: queryKeys.tutor.usage, queryFn: () => api.get<AiUsage>('/learning/ai/usage'), staleTime: 30_000 })
}

export type TutorAction = 'Ask' | 'Teach' | 'Next' | 'Quiz' | 'MultipleChoice' | 'Answer' | 'Hint' | 'Simplify' | 'Reveal'
export type TutorDifficulty = 'Easy' | 'Medium' | 'Hard'
export interface TutorTopic {
  key: string; title: string; pageFrom: number | null; pageTo: number | null; steps: number
  status: 'NotStarted' | 'InProgress' | 'Mastered'; currentStep: number; attempts: number; correct: number; partial: number; weak: boolean; awaitingAnswer: boolean
}

/** Topics of the chat's document with this user's progress; loaded only in teacher mode. */
export function useTopics(chatId: string | null, enabled: boolean) {
  return useQuery({
    queryKey: queryKeys.tutor.topics(chatId ?? ''),
    queryFn: () => api.get<TutorTopic[]>(`/learning/chats/${chatId}/topics`),
    enabled: !!chatId && enabled,
    staleTime: Infinity, // kept current from each answer's "done" event, no polling
  })
}
