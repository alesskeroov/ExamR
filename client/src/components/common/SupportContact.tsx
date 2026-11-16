import { Check, Copy } from 'lucide-react'
import { useEffect, useState } from 'react'
import { SUPPORT_EMAIL, supportMailto, type SupportTopic } from '../../lib/support'
import { cn } from '../../lib/utils'
import { useTranslation } from 'react-i18next'

interface Props extends Partial<SupportTopic> {
  /** Sentence before the address. */
  lead?: string
  className?: string
}

/**
 * The support address as a mailto link with a copy button: without a mail app the link opens nothing,
 * so the address itself is always visible. Colors follow the surrounding text (alerts, footers).
 */
export function SupportContact({ topic: topicProp, reference, lead: leadProp, className }: Props) {
  const { t } = useTranslation()
  const topic = topicProp ?? t('common.support.topic')
  const lead = leadProp ?? t('common.support.lead')
  const [copied, setCopied] = useState(false)
  useEffect(() => {
    if (!copied) return
    const timer = setTimeout(() => setCopied(false), 2000)
    return () => clearTimeout(timer)
  }, [copied])

  const copy = () => {
    navigator.clipboard?.writeText(SUPPORT_EMAIL).then(() => setCopied(true), () => { /* the address stays visible to copy by hand */ })
  }

  return (
    <p className={cn('text-sm leading-6', className)}>
      {lead}{' '}
      <a href={supportMailto({ topic, reference })} onClick={event => { event.currentTarget.href = supportMailto({ topic, reference }) }}
        className="font-semibold break-words underline underline-offset-2">{SUPPORT_EMAIL}</a>
      {' '}
      <button type="button" onClick={copy} aria-label={copied ? t('common.support.copiedAria') : t('common.support.copyAria')}
        className="inline-flex items-center gap-1 whitespace-nowrap rounded px-1 align-middle text-xs font-medium opacity-75 transition-opacity hover:opacity-100">
        {copied ? <Check className="size-3.5" aria-hidden /> : <Copy className="size-3.5" aria-hidden />}
        <span aria-live="polite">{copied ? t('common.support.copied') : t('common.support.copy')}</span>
      </button>
    </p>
  )
}
