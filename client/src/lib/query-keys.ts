export const queryKeys = {
  health: ['health'] as const,
  me: ['me'] as const,
  documents: {
    mine: ['documents', 'mine'] as const,
    completed: ['documents', 'completed'] as const,
    detail: (id: string) => ['documents', 'detail', id] as const,
  },
  questions: {
    byDocument: (documentId: string, filter: string) => ['questions', 'byDocument', documentId, filter] as const,
  },
  exams: {
    all: ['exams'] as const,
    active: ['exams', 'active'] as const,
    availability: (documentIds: string[]) => ['exams', 'availability', ...documentIds] as const,
    history: (page: number) => ['exams', 'history', page] as const,
    session: (id: string) => ['exams', 'session', id] as const,
    result: (id: string) => ['exams', 'result', id] as const,
  },
}
