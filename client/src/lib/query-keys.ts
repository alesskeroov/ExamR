export const queryKeys = {
  health: ['health'] as const,
  me: ['me'] as const,
  documents: {
    mine: ['documents', 'mine'] as const,
    detail: (id: string) => ['documents', 'detail', id] as const,
  },
  questions: {
    byDocument: (documentId: string, filter: string) => ['questions', 'byDocument', documentId, filter] as const,
  },
}
