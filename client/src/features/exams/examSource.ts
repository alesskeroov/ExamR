import type { ExamSource } from './api'
import i18n from '../../lib/i18n'

/** "unec-sample.pdf", or a fallback for deleted PDFs and older all-PDF exams. */
export function sourceName(source: ExamSource) {
  if (source.documentName) return source.documentName
  return source.documentId ? i18n.t('exam.source.deleted') : i18n.t('exam.source.all')
}

/** "150–200" or null when the exam had no range (older exams). */
export function sourceRange(source: ExamSource) {
  return source.rangeFrom !== null && source.rangeTo !== null ? `${source.rangeFrom}–${source.rangeTo}` : null
}
