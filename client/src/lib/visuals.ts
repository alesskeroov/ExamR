/** An image of a question or option, as sent by the API (file URL depends on where the question came from). */
export interface VisualDto {
  id: string
  /** Option the image belongs to; null = question body. */
  optionId: string | null
  pageNumber: number
  /** Size on the PDF page in points. */
  width: number
  height: number
  /** False when the image could not be converted; the UI points to the PDF page instead. */
  hasFile: boolean
  needsReview: boolean
}

/** Same text as the parser's placeholder for questions whose stem is only an image. */
export const IMAGE_ONLY_QUESTION_TEXT = '[Şəkil]'

/** Question or option text to show; null when the text is only a placeholder for its images (e.g. "." or "[Şəkil]"). */
export function visibleText(text: string, hasVisuals: boolean): string | null {
  if (!hasVisuals) return text
  const trimmed = text.trim()
  return trimmed === IMAGE_ONLY_QUESTION_TEXT || !/[\p{L}\p{N}]/u.test(trimmed) ? null : text
}

export const bodyVisuals = (visuals: VisualDto[] | undefined) => (visuals ?? []).filter((v) => !v.optionId)

export const optionVisuals = (visuals: VisualDto[] | undefined, optionId: string) =>
  (visuals ?? []).filter((v) => v.optionId === optionId)

/** File URLs; each endpoint checks the same permissions as the question it belongs to. */
export const visualUrls = {
  document: (documentId: string) => (id: string) => `/api/documents/${documentId}/visuals/${id}`,
  exam: (examId: string) => (id: string) => `/api/exams/${examId}/visuals/${id}`,
  battle: (code: string) => (id: string) => `/api/battles/rooms/${encodeURIComponent(code)}/visuals/${id}`,
  /** Admin explanation bank: every completed PDF, not only the admin's own. */
  explanationBank: (documentId: string) => (id: string) => `/api/admin/explanations/documents/${documentId}/visuals/${id}`,
}
