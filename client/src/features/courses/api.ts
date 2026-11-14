import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../lib/api-client'
import { queryKeys } from '../../lib/query-keys'

export type CourseRole = 'Material' | 'Bank'

/** A subject ("Fənn"): lecture materials, question banks and the exam date. daysLeft: null without a date or after it. */
export interface CourseSummary { id: string; name: string; examDate: string | null; daysLeft: number | null; materialCount: number; bankCount: number }
/** status/chatId: the material's guide index and the student's guide entry; questionCount: a bank's questions. */
export interface CourseDocument { documentId: string; name: string; role: CourseRole; format: string; status: string | null; chatId: string | null; questionCount: number | null }
export interface CourseDetail { id: string; name: string; examDate: string | null; daysLeft: number | null; documents: CourseDocument[] }
export interface CourseCandidate { documentId: string; name: string; format: string; role: CourseRole; uploadedAt: string }

export interface MaterialProgress { documentId: string; name: string; format: string; chatId: string | null; ready: boolean; cards: number; mastered: number; weak: number }
export interface BankProgress { documentId: string; name: string; questions: number; answered: number; correct: number; wrong: number }
export interface WeakSpot {
  materialDocumentId: string; materialName: string; format: string; chatId: string | null; section: string | null
  pageFrom: number | null; pageTo: number | null; chunkOrdinal: number; wrongAnswers: number
}
export type CourseTaskKind = 'Mistakes' | 'Read' | 'Cards' | 'Test' | 'Done'
export interface CourseTask {
  kind: CourseTaskKind; title: string; detail: string; documentId: string | null; chatId: string | null; count: number | null; chunkOrdinal: number | null
}
/** readiness: 0–100, mastered cards and correctly answered bank questions. */
export interface CoursePlan { daysLeft: number | null; readiness: number; materials: MaterialProgress[]; banks: BankProgress[]; weakSpots: WeakSpot[]; today: CourseTask[] }

/** Where in the course's lectures a missed question of a finished exam is explained; position as in the exam result. */
export interface ExamQuestionLink {
  position: number; materialDocumentId: string; materialName: string; format: string; chatId: string | null; section: string | null
  pageFrom: number | null; pageTo: number | null; chunkOrdinal: number; excerpt: string
}

export interface CourseInput { name: string; examDate: string | null }

export function useCourses() {
  return useQuery({ queryKey: queryKeys.courses.list, queryFn: () => api.get<CourseSummary[]>('/courses') })
}

export function useCourse(id: string) {
  return useQuery({ queryKey: queryKeys.courses.detail(id), queryFn: () => api.get<CourseDetail>(`/courses/${id}`), retry: false })
}

export function useCoursePlan(id: string) {
  return useQuery({ queryKey: queryKeys.courses.plan(id), queryFn: () => api.get<CoursePlan>(`/courses/${id}/plan`), retry: false })
}

export function useCourseCandidates(enabled: boolean) {
  return useQuery({ queryKey: queryKeys.courses.candidates, queryFn: () => api.get<CourseCandidate[]>('/courses/candidates'), enabled })
}

/** Links for a finished exam's missed questions; empty when its bank is in none of the student's courses. */
export function useExamLinks(examId: string) {
  return useQuery({
    queryKey: queryKeys.courses.examLinks(examId),
    queryFn: () => api.get<ExamQuestionLink[]>(`/courses/exam-links/${examId}`),
    staleTime: 5 * 60_000,
    retry: false,
  })
}

export function useSaveCourse() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ id, ...input }: CourseInput & { id?: string }) =>
      id ? api.put<CourseDetail>(`/courses/${id}`, input) : api.post<CourseDetail>('/courses', input),
    onSuccess: () => client.invalidateQueries({ queryKey: queryKeys.courses.all }),
  })
}

export function useSetCourseDocuments(id: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (input: { materials: string[]; banks: string[] }) => api.put<CourseDetail>(`/courses/${id}/documents`, input),
    onSuccess: () => client.invalidateQueries({ queryKey: queryKeys.courses.all }),
  })
}

export function useDeleteCourse() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.delete(`/courses/${id}`),
    onSuccess: () => client.invalidateQueries({ queryKey: queryKeys.courses.all }),
  })
}

/** What a guide link opens: a tab and scope of the material's guide, or a place in it ("at" = indexed part, page). */
export interface GuideTarget { tab?: string; scope?: string; at?: number; page?: number | null }
