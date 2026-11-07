import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, ApiError } from '../../lib/api-client'
import { queryKeys } from '../../lib/query-keys'
import type { PagedResult } from '../documents/api'
import type { VisualDto } from '../../lib/visuals'

export type RevealMode = 'Immediate' | 'AtEnd'
export type ExamStatus = 'InProgress' | 'Completed'
export type CompletionReason = 'Submitted' | 'TimeExpired'
export type ExamOutcome = 'Correct' | 'Incorrect' | 'Unanswered' | 'Ungraded'

export interface ExamOption {
  id: string
  label: string
  text: string
}

export interface AnswerReveal {
  correctOptionId: string | null
  isGradable: boolean
}

export interface ExamSessionQuestion {
  examQuestionId: string
  position: number
  text: string
  options: ExamOption[]
  selectedOptionId: string | null
  reveal: AnswerReveal | null
  /** Missing in responses cached before images were supported. */
  visuals?: VisualDto[]
  /** Answer locked by the server once it was shown (Immediate exams): it can no longer be changed. */
  locked: boolean
}

export interface ExamSession {
  id: string
  status: ExamStatus
  revealMode: RevealMode
  questionCount: number
  startedAt: string
  expiresAt: string
  serverNow: string
  questions: ExamSessionQuestion[]
}

export interface ExamResultQuestion {
  position: number
  text: string
  options: ExamOption[]
  selectedOptionId: string | null
  correctOptionId: string | null
  outcome: ExamOutcome
  visuals?: VisualDto[]
}

/** Source PDF and question range. documentName is null if the PDF was deleted; range is null on older exams. */
export interface ExamSource {
  documentId: string | null
  documentName: string | null
  rangeFrom: number | null
  rangeTo: number | null
}

export interface ExamResult {
  id: string
  questionCount: number
  revealMode: RevealMode
  /** Time limit the exam had, in minutes (a retake starts from it). */
  timeLimitMinutes: number
  source: ExamSource
  completionReason: CompletionReason
  correct: number
  incorrect: number
  unanswered: number
  ungraded: number
  scorePercent: number | null
  startedAt: string
  completedAt: string
  durationSeconds: number
  questions: ExamResultQuestion[]
}

export interface ExamSummary {
  id: string
  status: ExamStatus
  questionCount: number
  revealMode: RevealMode
  source: ExamSource
  startedAt: string
  expiresAt: string
  completedAt: string | null
  completionReason: CompletionReason | null
  correct: number | null
  incorrect: number | null
  unanswered: number | null
  ungraded: number | null
  scorePercent: number | null
}

export interface ExamAvailability {
  available: number
  ungraded: number
  /** First and last original question number of the PDF. */
  firstNumber: number | null
  lastNumber: number | null
  maxQuestions: number
  /** Time limit used when none is chosen. */
  defaultDurationMinutes: number
}

export interface CreateExamRequest {
  questionCount: number
  documentId: string
  rangeFrom: number
  rangeTo: number
  revealMode: RevealMode
  /** Omitted = defaultDurationMinutes. */
  durationMinutes?: number
}

/** Question-number span of a PDF and how many questions a range of it holds (no range = whole PDF). */
export function useExamAvailability(documentId: string, range?: { from: number; to: number }) {
  const params = new URLSearchParams({ documentId })
  if (range) {
    params.set('from', String(range.from))
    params.set('to', String(range.to))
  }
  return useQuery({
    queryKey: queryKeys.exams.availability(documentId, range?.from, range?.to),
    queryFn: () => api.get<ExamAvailability>(`/exams/availability?${params}`),
    enabled: documentId !== '',
    placeholderData: keepPreviousData,
  })
}

export function useActiveExam() {
  return useQuery({
    queryKey: queryKeys.exams.active,
    queryFn: async () => {
      try {
        return (await api.get<{ id: string } | undefined>('/exams/active')) ?? null
      } catch (error) {
        if (error instanceof ApiError && error.status === 404) return null
        throw error
      }
    },
    staleTime: 0,
  })
}

export function useCreateExam() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: CreateExamRequest) => api.post<ExamSession>('/exams', request),
    onSuccess: (session) => {
      queryClient.setQueryData(queryKeys.exams.session(session.id), session)
      void queryClient.invalidateQueries({ queryKey: queryKeys.exams.all })
    },
  })
}

export function useExamSession(id: string) {
  return useQuery({
    queryKey: queryKeys.exams.session(id),
    queryFn: () => api.get<ExamSession>(`/exams/${id}`),
    staleTime: Infinity, // Answers are kept in the cache optimistically; no background refetches.
  })
}

interface AnswerVariables {
  examQuestionId: string
  selectedOptionId: string | null
}

interface AnswerResponse {
  examQuestionId: string
  selectedOptionId: string | null
  reveal: AnswerReveal | null
  locked: boolean
}

export const answerMutationKey = (examId: string) => ['exams', 'answer', examId] as const

/** Saves an answer immediately; the UI updates optimistically and rolls back if the server rejects it. */
const answerSequences: Record<string, Map<string, number>> = {}

export function useAnswerQuestion(examId: string) {
  const queryClient = useQueryClient()
  const key = queryKeys.exams.session(examId)

  const patch = (examQuestionId: string, change: Partial<ExamSessionQuestion>) =>
    queryClient.setQueryData<ExamSession>(key, (session) =>
      session && {
        ...session,
        questions: session.questions.map((q) => (q.examQuestionId === examQuestionId ? { ...q, ...change } : q)),
      },
    )

  // Latest pick per question: a slower, older response must not overwrite a newer choice.
  const latest = (answerSequences[examId] ??= new Map<string, number>())

  return useMutation({
    mutationKey: answerMutationKey(examId),
    // Answer requests of one exam run one after another, so the server always stores the last pick.
    scope: { id: `exam-answer:${examId}` },
    mutationFn: ({ examQuestionId, selectedOptionId }: AnswerVariables) =>
      api.put<AnswerResponse>(`/exams/${examId}/answers/${examQuestionId}`, { selectedOptionId }),
    onMutate: ({ examQuestionId, selectedOptionId }) => {
      const sequence = (latest.get(examQuestionId) ?? 0) + 1
      latest.set(examQuestionId, sequence)
      patch(examQuestionId, { selectedOptionId })
      return { sequence }
    },
    onSuccess: (response, { examQuestionId }, context) => {
      if (latest.get(examQuestionId) !== context.sequence) return
      patch(response.examQuestionId, { selectedOptionId: response.selectedOptionId, reveal: response.reveal, locked: response.locked })
    },
    onError: (_error, { examQuestionId }, context) => {
      if (!context || latest.get(examQuestionId) !== context.sequence) return
      // Show what the server really stored instead of guessing an earlier optimistic value.
      void queryClient.invalidateQueries({ queryKey: key })
    },
  })
}

/** Immediate exams: shows the correct answer and locks the question on the server. */
export function useRevealAnswer(examId: string) {
  const queryClient = useQueryClient()
  const key = queryKeys.exams.session(examId)
  return useMutation({
    // Same scope as answers: a reveal waits for a pending answer of this exam, so the server sees them in order.
    scope: { id: `exam-answer:${examId}` },
    mutationFn: (examQuestionId: string) => api.post<AnswerResponse>(`/exams/${examId}/questions/${examQuestionId}/reveal`),
    onSuccess: (response) =>
      queryClient.setQueryData<ExamSession>(key, (session) =>
        session && {
          ...session,
          questions: session.questions.map((q) => (q.examQuestionId === response.examQuestionId
            ? { ...q, selectedOptionId: response.selectedOptionId, reveal: response.reveal, locked: response.locked }
            : q)),
        },
      ),
    onError: () => void queryClient.invalidateQueries({ queryKey: key }),
  })
}

export function useFinishExam(examId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => api.post<ExamResult>(`/exams/${examId}/finish`),
    onSuccess: (result) => {
      queryClient.setQueryData(queryKeys.exams.result(examId), result)
      queryClient.removeQueries({ queryKey: queryKeys.exams.session(examId) })
      void queryClient.invalidateQueries({ queryKey: queryKeys.exams.all })
    },
  })
}

export function useExamResult(id: string) {
  return useQuery({
    queryKey: queryKeys.exams.result(id),
    queryFn: () => api.get<ExamResult>(`/exams/${id}/result`),
    enabled: id !== '',
    staleTime: Infinity,
    retry: false,
  })
}

export function useExamHistory(page: number) {
  return useQuery({
    queryKey: queryKeys.exams.history(page),
    queryFn: () => api.get<PagedResult<ExamSummary>>(`/exams?page=${page}&pageSize=20`),
    placeholderData: keepPreviousData,
  })
}
