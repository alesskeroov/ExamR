import type { ExamSessionQuestion } from './api'

/**
 * Whether an open answer can be saved. Like the server, anything but blank is accepted: numbers (12, 2,5, 5/2, 80°),
 * coordinates ((3; -4)) and words (Azerbaijani language tasks) are all graded there.
 */
export function isCodedAnswer(text: string) {
  return /[\p{L}\p{N}]/u.test(text)
}

export function isAnswered(question: ExamSessionQuestion) {
  return question.isCoded ? !!question.answerText : !!question.selectedOptionId
}
