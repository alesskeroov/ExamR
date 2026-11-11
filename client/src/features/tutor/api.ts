import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, uploadFile } from '../../lib/api-client'
import { queryKeys } from '../../lib/query-keys'
import type { PagedResult } from '../documents/api'

export function useSendFeedback() {
  return useMutation({
    mutationFn: (body: { requestId: string; kind: 'problem' | 'idea'; text: string; documentName?: string; pagePath: string }) =>
      api.post<{ id: string }>('/feedback', body),
    retry: false,
  })
}

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
    onSettled: () => client.invalidateQueries({ queryKey: queryKeys.usage.mine }),
    mutationFn: (documentId: string) => api.post<ChatSession>('/learning/chats', { documentId }),
    onSuccess: () => client.invalidateQueries({ queryKey: queryKeys.tutor.chats }),
  })
}

export function useDeleteChat() {
  const client = useQueryClient()
  return useMutation({
    onSettled: () => client.invalidateQueries({ queryKey: queryKeys.usage.mine }),
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
    onSettled: () => client.invalidateQueries({ queryKey: queryKeys.usage.mine }),
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

/** Unchecked = "Yoxlanılmadı": no reliable verdict (or a technical problem; then canRetry). */
export type QuizStatus = 'Correct' | 'Incorrect' | 'Unchecked'
export interface QuizQuestion { id: string; question: string; pageFrom: number | null; pageTo: number | null; number: number }
export interface QuizResult {
  id: string; question: string; status: QuizStatus; feedback: string; correctAnswer: string; missingPoints: string[]
  pageFrom: number | null; pageTo: number | null; aiChecked: boolean; canRetry: boolean
}
export interface QuizReviewItem { number: number; question: string; correctAnswer: string; page: number | null; answeredAt: string }
export interface WeakTopic { title: string; numbers: number[]; wrong: number; answered: number }
/** By each question's latest answer. `review` = questions last answered wrong (stored per user and document). */
/** pending: the newest question was asked but never answered ("Qaldığın yerdən davam et"). */
export interface QuizReview {
  correct: number; incorrect: number; unchecked: number; questions: number; review: QuizReviewItem[]; weakTopics: WeakTopic[]; pending: QuizQuestion | null
}
export interface OpenQuestion { number: number; text: string; chunkOrdinal: number; page: number | null; hasAnswer: boolean }
export interface OpenQuestions { supported: boolean; warning: string | null; questions: OpenQuestion[] }
export type AnswerLength = 'Short' | 'Long'
export interface SharedPayload { generatorVersion: string; mainTopic: string; answer: string; sourcePages: number[] }
export interface LastMinuteSheet {
  topics: { title: string; explanation: string; remember: string[]; related: string[] }[]; keyTerms: string[]
  sourcePages: number[]; readingMinutes: number; fromQuestions: boolean
}
export interface Material<T = unknown> {
  id: string; kind: 'SharedAnswer' | 'LastMinute'; length: AnswerLength | null; minutes: number | null; saved: boolean; fromCache: boolean; createdAt: string; payload: T
}
export interface MaterialSummary { id: string; kind: 'SharedAnswer' | 'LastMinute'; length: AnswerLength | null; minutes: number | null; saved: boolean; title: string; items: number; createdAt: string; generatorVersion?: string | null }

export function useQuizReview(chatId: string) {
  return useQuery({ queryKey: queryKeys.tutor.review(chatId), queryFn: () => api.get<QuizReview>(`/learning/chats/${chatId}/quiz/review`) })
}
/** `numbers`: only these questions (a Hazırlaş topic or the review list). `asked`: already asked in this session, never repeated. */
export function useNextQuestion(chatId: string) {
  const client = useQueryClient()
  return useMutation({
    onSettled: () => client.invalidateQueries({ queryKey: queryKeys.usage.mine }),
    mutationFn: ({ numbers = [], asked = [] }: { numbers?: number[]; asked?: number[] }) => {
      const query = [...numbers.map(n => `numbers=${n}`), ...asked.map(n => `asked=${n}`)].join('&')
      return api.post<QuizQuestion>(`/learning/chats/${chatId}/quiz${query ? `?${query}` : ''}`)
    },
  })
}
export function useAnswerQuestion(chatId: string) {
  const client = useQueryClient()
  return useMutation({
    onSettled: () => client.invalidateQueries({ queryKey: queryKeys.usage.mine }),
    mutationFn: ({ id, answer, giveUp }: { id: string; answer?: string; giveUp?: boolean }) => api.post<QuizResult>(`/learning/quiz/${id}/answer`, { answer, giveUp }),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: queryKeys.tutor.review(chatId) })
      void client.invalidateQueries({ queryKey: queryKeys.tutor.revision(chatId) }) // weak topics move to the top of Son təkrar
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
    onSettled: () => client.invalidateQueries({ queryKey: queryKeys.usage.mine }),
    mutationFn: (body: TBody) => api.post<Material<TPayload>>(`/learning/chats/${chatId}/${path}`, body),
    onSuccess: material => {
      client.setQueryData(queryKeys.tutor.material(material.id), material)
      void client.invalidateQueries({ queryKey: queryKeys.tutor.materials(chatId) })
      void client.invalidateQueries({ queryKey: queryKeys.tutor.usage })
    },
  })
}
export const useSharedAnswers = (chatId: string) =>
  useMaterialMutation<{ regenerate?: boolean }, SharedPayload>(chatId, 'shared-answers')
export const useLastMinute = (chatId: string) => useMaterialMutation<{ minutes: number }, LastMinuteSheet>(chatId, 'last-minute')
export function useSaveMaterial(chatId: string) {
  const client = useQueryClient()
  return useMutation({
    onSettled: () => client.invalidateQueries({ queryKey: queryKeys.usage.mine }),
    mutationFn: ({ id, saved }: { id: string; saved: boolean }) => api.post<void>(`/learning/materials/${id}/save`, { saved }),
    onSuccess: (_, { id, saved }) => {
      client.setQueryData<Material>(queryKeys.tutor.material(id), m => m && { ...m, saved })
      void client.invalidateQueries({ queryKey: queryKeys.tutor.materials(chatId) })
    },
  })
}
// ---------- Son təkrar ----------

export interface RevisionCard {
  facts: string[]; concepts: { term: string; meaning: string }[]; comparisons: { first: string; second: string; difference: string }[]
  tips: string[]; sourcePages: number[]
}
export interface RevisionTopic {
  key: string; title: string; questions: number; wrong: number; answered: number; numbers: number[]; wrongNumbers: number[]; card: RevisionCard | null
}
/** hasHistory: ordered by the student's Məni yoxla results (weak topics first); otherwise a neutral order. */
export interface Revision { hasHistory: boolean; warning: string | null; topics: RevisionTopic[]; error: string | null }

/** Topics in revision order with their stored cards; the server prepares cards only for the first topics that still lack one. */
export function useRevision(chatId: string) {
  return useQuery({
    queryKey: queryKeys.tutor.revision(chatId),
    queryFn: () => api.post<Revision>(`/learning/chats/${chatId}/revision`, {}),
    staleTime: 60_000, retry: false,
  })
}

/** "Daha çox": prepares the cards of the given topics (one AI call for the batch) and replaces the cached list. */
export function useMoreRevision(chatId: string) {
  const client = useQueryClient()
  return useMutation({
    onSettled: () => client.invalidateQueries({ queryKey: queryKeys.usage.mine }),
    mutationFn: (keys: string[]) => api.post<Revision>(`/learning/chats/${chatId}/revision`, { keys }),
    onSuccess: data => {
      client.setQueryData(queryKeys.tutor.revision(chatId), data)
      void client.invalidateQueries({ queryKey: queryKeys.tutor.usage })
    },
  })
}

// ---------- Hazırlaş ----------

export type PrepareView = 'explain' | 'answer'
export interface PrepareQuestion { number: number; text: string; page: number | null }
/** section: the broad main topic ("" when unknown). */
export interface PrepareTopic { key: string; title: string; questions: PrepareQuestion[]; explained: boolean; answered: boolean; section: string }
/** found = every numbered question in the document; usable = with the document's own answer. */
export interface PrepareStats { found: number; usable: number; sharedAnswers: number; noAnswer: number; multipleChoice: number; unclear: number }
export interface ExcludedQuestion { number: number; text: string; page: number | null; reason: string }
export interface PrepareTopics { warning: string | null; topics: PrepareTopic[]; stats: PrepareStats | null; excluded: ExcludedQuestion[] | null }
export interface TopicExplanation {
  title: string; explanation: string[]; concepts: { term: string; meaning: string }[]; example: string | null; facts: string[]
  questionNumbers: number[]; sourcePages: number[]
}
export interface CoveredQuestion { number: number; text: string; page: number | null; covered: boolean; addition: string; documentAnswer: string | null }
export interface GroupAnswer { title: string; sufficient: boolean; answer: string; questions: CoveredQuestion[]; sourcePages: number[] }
export interface PrepareMaterial<T> { key: string; fromCache: boolean; createdAt: string; payload: T }

/** Topics of the document's open questions. A POST because the first call may group the questions with AI (then cached). */
export function usePrepareTopics(chatId: string) {
  return useQuery({
    queryKey: queryKeys.tutor.prepareTopics(chatId),
    queryFn: () => api.post<PrepareTopics>(`/learning/chats/${chatId}/prepare/topics`),
    staleTime: Infinity, retry: false,
  })
}

/** Explanation or exam answer of one topic; generated once per document version on the server, then served from its cache. */
export function usePrepareMaterial<T>(chatId: string, view: PrepareView, key: string) {
  const client = useQueryClient()
  return useQuery({
    queryKey: queryKeys.tutor.prepare(chatId, view, key),
    queryFn: async () => {
      const material = await api.post<PrepareMaterial<T>>(`/learning/chats/${chatId}/prepare/topics/${key}/${view === 'explain' ? 'explanation' : 'answer'}`, {})
      if (!material.fromCache) void client.invalidateQueries({ queryKey: queryKeys.tutor.usage })
      return material
    },
    staleTime: Infinity, retry: false,
  })
}

export function useDeleteMaterial(chatId: string) {
  const client = useQueryClient()
  return useMutation({
    onSettled: () => client.invalidateQueries({ queryKey: queryKeys.usage.mine }),
    mutationFn: (id: string) => api.delete(`/learning/materials/${id}`),
    onSuccess: () => client.invalidateQueries({ queryKey: queryKeys.tutor.materials(chatId) }),
  })
}

// ---------- Öyrənmə bələdçisi (no AI) ----------

export type CardGrade = 'Again' | 'Hard' | 'Good'
/**
 * Question = open question; Term = "X — definition" from an answer; List = "…:" with items; Test = test question and its
 * correct option; Table = a table row or comparison; Custom = a card the student wrote.
 */
export type CardKind = 'Question' | 'Term' | 'List' | 'Test' | 'Table' | 'Custom'
/**
 * One flashcard and the student's state. items: list items (List), terms explained in the answer (Question) or the
 * explanation (Test); others: wrong test options; source: the document's question number. weak: last answered "Bilmirdim"/"Qismən".
 */
export interface StudyCard {
  id: string; kind: CardKind; topicKey: string; front: string; back: string; items: string[]; others: string[]
  source: number; page: number | null; chunkOrdinal: number; isSection: boolean; section: string | null; seen: boolean; streak: number; mastered: boolean; weak: boolean
  /** The extraction is unsure of this card ("Yoxlanmalı"). */
  needsReview: boolean
  /** own: written by the student; edited: a document card the student rewrote; hidden: kept out of study by the student. */
  own: boolean; edited: boolean; hidden: boolean
}
export interface CardTopic { key: string; title: string; notes: string | null }
/** The student's private deck: topics in document order and all cards. required = "Bilirdim" in a row to master a card. */
export interface CardDeck { warning: string | null; required: number; topics: CardTopic[]; cards: StudyCard[] }
/** What Öyrən studies: every card, weak cards, all mixed, or one topic. */
export type LearnScope = { kind: 'all' } | { kind: 'weak' } | { kind: 'mixed' } | { kind: 'topic'; key: string }
export interface CardState { id: string; streak: number; mastered: boolean; weak: boolean }

export function useDeck(chatId: string) {
  return useQuery({ queryKey: queryKeys.tutor.deck(chatId), queryFn: () => api.get<CardDeck>(`/learning/chats/${chatId}/deck`), retry: false })
}

/** Saves the student's own grade and patches the cached deck, so Mövzular and Kartlarım stay current during a session. */
export function useReviewCard(chatId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ id, grade }: { id: string; grade: CardGrade }) =>
      api.post<CardState>(`/learning/chats/${chatId}/cards/${id}/review`, { grade }),
    onSuccess: state => client.setQueryData<CardDeck>(queryKeys.tutor.deck(chatId), deck => deck && {
      ...deck, cards: deck.cards.map(c => c.id === state.id ? { ...c, ...state, seen: true } : c),
    }),
  })
}

export interface CardInput { front: string; back: string; topicKey?: string | null }

/** Replaces one card in the cached deck (or adds it), so every tab shows the change without a refetch. */
function patchCard(card: StudyCard) {
  return (deck: CardDeck | undefined) => deck && {
    ...deck,
    topics: deck.topics.some(t => t.key === card.topicKey) ? deck.topics : [...deck.topics, { key: card.topicKey, title: 'Mənim kartlarım', notes: null }],
    cards: deck.cards.some(c => c.id === card.id) ? deck.cards.map(c => c.id === card.id ? card : c) : [...deck.cards, card],
  }
}

/** Creates an own card (no id) or rewrites a card (own or from the document). */
export function useSaveCard(chatId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ id, ...input }: CardInput & { id?: string }) => id
      ? api.put<StudyCard>(`/learning/chats/${chatId}/cards/${encodeURIComponent(id)}`, input)
      : api.post<StudyCard>(`/learning/chats/${chatId}/cards`, input),
    onSuccess: card => client.setQueryData<CardDeck>(queryKeys.tutor.deck(chatId), patchCard(card)),
  })
}

export function useHideCard(chatId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ id, hidden }: { id: string; hidden: boolean }) =>
      api.put<StudyCard>(`/learning/chats/${chatId}/cards/${encodeURIComponent(id)}/hidden`, { hidden }),
    onSuccess: card => client.setQueryData<CardDeck>(queryKeys.tutor.deck(chatId), patchCard(card)),
  })
}

/** Deletes for good: an own card is removed, a document card leaves this student's deck. */
export function useDeleteCard(chatId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.delete(`/learning/chats/${chatId}/cards/${encodeURIComponent(id)}`),
    onSuccess: (_, id) => client.setQueryData<CardDeck>(queryKeys.tutor.deck(chatId), deck => deck && { ...deck, cards: deck.cards.filter(c => c.id !== id) }),
  })
}

/** Cards the student studies: hidden ones are left out of Öyrən and the progress counts. */
export function studyCards(deck: CardDeck) {
  return deck.cards.filter(c => !c.hidden)
}

export interface TopicStats { total: number; mastered: number; weak: number; seen: number }
/** Card counts per topic key, for progress bars and the topic picker. */
export function topicStats(deck: CardDeck) {
  const stats = new Map<string, TopicStats>(deck.topics.map(t => [t.key, { total: 0, mastered: 0, weak: 0, seen: 0 }]))
  for (const card of studyCards(deck)) {
    const s = stats.get(card.topicKey)
    if (!s) continue
    s.total += 1
    if (card.mastered) s.mastered += 1
    if (card.weak) s.weak += 1
    if (card.seen) s.seen += 1
  }
  return stats
}
