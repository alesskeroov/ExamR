import i18n from '../../lib/i18n'
/** Where a passage is: "slayd 12" for presentations, "səhifə 4–5" otherwise; null when the file has no pages (DOCX). */
export function placeLabel(format: string, from: number | null, to: number | null) {
  if (from == null) return null
  const unit = format === 'pptx' ? i18n.t('tutor.place.slide') : i18n.t('tutor.place.page')
  return `${unit} ${from}${to != null && to !== from ? `–${to}` : ''}`
}
