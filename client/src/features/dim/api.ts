import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, ApiError } from '../../lib/api-client'
import { queryKeys } from '../../lib/query-keys'
import type { ExamStatus, RevealMode } from '../exams/api'

export type DimTrack = 'Buraxilis' | 'Blok'
export type DimTestKind = 'Full' | 'Subject'

export interface DimSubject { code: string; name: string; closed: number; coded: number; written: number; wrongPenalty: boolean }
export interface DimGroup { code: string; name: string; subjects: { code: string; name: string; coefficient: number }[] }
export interface DimCatalog { buraxilis: DimSubject[]; blok: DimSubject[]; groups: DimGroup[] }

export interface DimSection { subject: string; name: string; questionCount: number; random: boolean; sourceDocumentId?: string | null }

export interface DimTest {
  id: string
  track: DimTrack
  kind: DimTestKind
  groupCode: string | null
  groupName: string | null
  title: string
  durationMinutes: number
  questionCount: number
  sections: DimSection[]
  attemptLimit: number | null
  attemptsUsed: number
  availableFrom: string | null
  availableTo: string | null
  isOpen: boolean
  lastAttempt: { examId: string; status: ExamStatus; startedAt: string; scorePercent: number | null } | null
}

export interface DimSubjectScore {
  subject: string
  name: string
  coefficient: number
  total: number
  correct: number
  incorrect: number
  unanswered: number
  ungraded: number
  /** Points of the measured parts under the official formula; 0/0 when the test had no such questions. */
  closedPoints: number
  closedMaxPoints: number
  openTotal: number
  openCorrect: number
  openPoints: number
  openMaxPoints: number
  measuredPoints: number
  measuredMaxPoints: number
  estimatedPoints: number
  fullMaxPoints: number
}

export interface DimReport {
  examId: string
  testId: string
  title: string
  track: DimTrack
  kind: DimTestKind
  groupCode: string | null
  groupName: string | null
  correct: number
  incorrect: number
  unanswered: number
  ungraded: number
  scorePercent: number | null
  score: {
    subjects: DimSubjectScore[]
    closedPoints: number
    closedMaxPoints: number
    openPoints: number
    openMaxPoints: number
    measuredPoints: number
    measuredMaxPoints: number
    estimatedPoints: number
    fullMaxPoints: number
    weakTopics: { subject: string; topic: string; total: number; correct: number; accuracy: number }[]
  }
  notice: string
  /** null = unlimited attempts. */
  attemptLimit: number | null
  attemptsUsed: number
  /** Test still published and inside its availability window. */
  canStart: boolean
}

export function useDimCatalog() {
  return useQuery({
    queryKey: queryKeys.dim.catalog,
    queryFn: () => api.get<DimCatalog>('/dim/catalog'),
    staleTime: Infinity,
  })
}

export function useDimTests(track: DimTrack) {
  return useQuery({
    queryKey: queryKeys.dim.tests(track),
    queryFn: () => api.get<DimTest[]>(`/dim/tests?track=${track}`),
    // Attempts can end by time on the server while this page is not open; always re-check on visit.
    staleTime: 0,
  })
}

export function useStartDimTest() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ testId, revealMode }: { testId: string; revealMode: RevealMode }) =>
      api.post<{ examId: string }>(`/dim/tests/${testId}/start`, { revealMode }),
    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.dim.all })
      void queryClient.invalidateQueries({ queryKey: queryKeys.exams.all })
    },
  })
}

/** DİM breakdown of a finished exam; null when the exam is not a DİM attempt. */
export function useDimReport(examId: string) {
  return useQuery({
    queryKey: queryKeys.dim.report(examId),
    queryFn: async () => {
      try {
        return await api.get<DimReport>(`/dim/attempts/${examId}/report`)
      } catch (error) {
        if (error instanceof ApiError && error.status === 404) return null
        throw error
      }
    },
    staleTime: Infinity,
    retry: false,
  })
}

// ---- Admin ----

export type VerificationStatus = 'Unverified' | 'AutoDetected' | 'Verified'

export interface DimSourceQuestion {
  id: string
  sourceNumber: number
  text: string
  options: { id: string; label: string; text: string; isCorrect: boolean }[]
  verificationStatus: VerificationStatus
  parseWarning: string | null
  bank: { track: DimTrack; subject: string; topic: string | null } | null
  /** Accepted answers of an open coded question (no options), e.g. "2,5; 5/2". */
  codedAnswer: string | null
}

/** A parsed document: `valid` questions fit DİM tests; `bank` lists how many are already in the bank per subject. */
export interface DimSourceDocument {
  id: string
  originalFileName: string
  createdAt: string
  questionCount: number
  valid: number
  bank: { track: DimTrack; subject: string; name: string; count: number }[]
  /** A set of questions typed in by hand (no file). */
  isManual: boolean
}

export interface DimAdminTest {
  id: string
  track: DimTrack
  kind: DimTestKind
  groupCode: string | null
  title: string
  durationMinutes: number
  attemptLimit: number | null
  availableFrom: string | null
  availableTo: string | null
  isPublished: boolean
  createdAt: string
  sections: DimSection[]
  attempts: number
}

export interface DimTestSettings {
  title: string
  durationMinutes: number
  attemptLimit: number | null
  availableFrom: string | null
  availableTo: string | null
  isPublished: boolean
}

export interface DimTestCreate extends DimTestSettings {
  track: DimTrack
  kind: DimTestKind
  groupCode: string | null
  sections: { subject: string; questionIds?: string[]; randomCount: number; drawPerAttempt: boolean; sourceDocumentId?: string | null }[]
}

export function useDimSourceDocuments() {
  return useQuery({
    queryKey: queryKeys.dim.sourceDocuments,
    queryFn: () => api.get<DimSourceDocument[]>('/admin/dim/documents'),
  })
}

export function useDimSourceQuestions(documentId: string) {
  return useQuery({
    queryKey: queryKeys.dim.sourceQuestions(documentId),
    queryFn: () => api.get<DimSourceQuestion[]>(`/admin/dim/documents/${documentId}/questions`),
    enabled: documentId !== '',
  })
}

export function useDimBankStats() {
  return useQuery({
    queryKey: queryKeys.dim.bankStats,
    queryFn: () => api.get<{ track: DimTrack; subject: string; name: string; count: number; topics: number }[]>('/admin/dim/bank/stats'),
  })
}

export function useDimAdminTests() {
  return useQuery({ queryKey: queryKeys.dim.adminTests, queryFn: () => api.get<DimAdminTest[]>('/admin/dim/tests') })
}

/** Every admin write may change the bank, tests or the public list, so all DİM queries are refreshed. */
function useDimAdminMutation<TVariables, TResult>(fn: (variables: TVariables) => Promise<TResult>) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: fn,
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: queryKeys.dim.all }),
  })
}

export const useApplyAnswerKey = () => useDimAdminMutation(({ documentId, text }: { documentId: string; text: string }) =>
  api.post<{ applied: number; notFound: number[]; invalid: number[]; conflicts: number[] }>(`/admin/dim/documents/${documentId}/answer-key`, { text }))

type ApproveResult = { approved: number; rejected: { questionId: string; reason: string }[] }

/** Adds every question of the document that fits DİM tests to the bank in one request. */
export const useApproveValid = () => useDimAdminMutation(({ documentId, ...body }: { documentId: string; track: DimTrack; subject: string; topic: string | null }) =>
  api.post<ApproveResult>(`/admin/dim/documents/${documentId}/approve-valid`, body))

/** Closed questions send `correctOptionId`; questions without options send `codedAnswer`. */
export interface DimQuestionEdit { id: string; text: string; options: { id: string; text: string }[]; correctOptionId?: string; codedAnswer?: string }

/** A question typed in by hand: no options = open coded question with `codedAnswer`. */
export interface DimQuestionCreate {
  documentId: string
  track: DimTrack
  subject: string
  topic: string | null
  text: string
  options: string[]
  correctIndex: number | null
  codedAnswer: string | null
}

export const useCreateDimSet = () => useDimAdminMutation((title: string) => api.post<DimSourceDocument>('/admin/dim/sets', { title }))

export const useCreateDimQuestion = () => useDimAdminMutation((request: DimQuestionCreate) => api.post<DimSourceQuestion>('/admin/dim/questions', request))

/** Only questions of a manual set can be deleted; PDF questions are removed from the bank instead. */
export const useDeleteDimQuestion = () => useDimAdminMutation((questionId: string) => api.delete(`/admin/dim/questions/${questionId}`))

export const useEditDimQuestion = () => useDimAdminMutation(({ id, ...body }: DimQuestionEdit) => api.put<void>(`/admin/dim/questions/${id}`, body))

export const useRemoveFromBank = () => useDimAdminMutation((questionId: string) => api.delete(`/admin/dim/bank/${questionId}`))

export const useCreateDimTest = () => useDimAdminMutation((request: DimTestCreate) => api.post<DimAdminTest>('/admin/dim/tests', request))

export const useUpdateDimTest = () => useDimAdminMutation(({ id, settings }: { id: string; settings: DimTestSettings }) =>
  api.put<DimAdminTest>(`/admin/dim/tests/${id}`, settings))

export function useDeleteDimTest() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.delete(`/admin/dim/tests/${id}`),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.dim.all })
      // Deleting a test also deletes its attempts, which are ordinary exams in the history.
      void queryClient.invalidateQueries({ queryKey: queryKeys.exams.all })
    },
  })
}
