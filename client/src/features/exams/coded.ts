import type { ExamSessionQuestion } from './api'

/** Same shapes the server accepts for a coded answer: 12, -3, 2,5, 2.5, ,5, 5/2 (spaces ignored). */
const NUMBER = /^[+-]?(\d+([.,]\d+)?|[.,]\d+)(\/\d+([.,]\d+)?)?$/

export function isCodedNumber(text: string) {
  return NUMBER.test(text.replace(/\s+/g, '').replace(/[−–]/g, '-'))
}

export function isAnswered(question: ExamSessionQuestion) {
  return question.isCoded ? !!question.answerText : !!question.selectedOptionId
}
