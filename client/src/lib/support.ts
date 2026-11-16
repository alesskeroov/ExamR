import i18n, { localeTag } from './i18n'
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
    i18n.t('common.support.mailSection', { path: window.location.pathname }),
    ...(reference ? [i18n.t('common.support.mailReference', { ref: reference })] : []),
    i18n.t('common.support.mailDate', { date: new Date().toLocaleString(localeTag(), { timeZone: 'Asia/Baku' }) }),
    '',
    i18n.t('common.support.mailBody'),
    '',
  ]
  return `mailto:${SUPPORT_EMAIL}?subject=${encodeURIComponent(subject)}&body=${encodeURIComponent(lines.join('\n'))}`
}
