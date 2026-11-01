import type { ExamSource } from './api'

/** "unec-sample.pdf", or a fallback for deleted PDFs and older all-PDF exams. */
export function sourceName(source: ExamSource) {
  if (source.documentName) return source.documentName
  return source.documentId ? 'Silinmiş PDF' : 'Bütün PDF-lər'
}

/** "150–200" or null when the exam had no range (older exams). */
export function sourceRange(source: ExamSource) {
  return source.rangeFrom !== null && source.rangeTo !== null ? `${source.rangeFrom}–${source.rangeTo}` : null
}
