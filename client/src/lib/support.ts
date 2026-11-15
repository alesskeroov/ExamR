/** The one support address shown across the site; override per deployment with VITE_SUPPORT_EMAIL. */
export const SUPPORT_EMAIL = import.meta.env.VITE_SUPPORT_EMAIL?.trim() || 'examr.contact@gmail.com'

export interface SupportTopic {
  /** Short subject, e.g. "İmtahan problemi". */
  topic: string
  /** An id that lets the team find the case (exam id, test name). Never a stack trace or a secret. */
  reference?: string
}

/** mailto link with the subject and a short template prefilled, so the message arrives with its context. */
export function supportMailto({ topic, reference }: SupportTopic) {
  const subject = `[ExamR] ${topic}${reference ? ` · ${reference}` : ''}`
  const lines = [
    `Bölmə: ${window.location.pathname}`,
    ...(reference ? [`İstinad: ${reference}`] : []),
    `Tarix: ${new Date().toLocaleString('az-AZ', { timeZone: 'Asia/Baku' })} (Bakı vaxtı)`,
    '',
    'Problemi qısa təsvir edin:',
    '',
  ]
  return `mailto:${SUPPORT_EMAIL}?subject=${encodeURIComponent(subject)}&body=${encodeURIComponent(lines.join('\n'))}`
}
