import { Check, Copy } from 'lucide-react'
import { useEffect, useState } from 'react'
import { SUPPORT_EMAIL, supportMailto, type SupportTopic } from '../../lib/support'
import { cn } from '../../lib/utils'

interface Props extends Partial<SupportTopic> {
  /** Sentence before the address. */
  lead?: string
  className?: string
}

/**
 * The support address as a mailto link with a copy button: without a mail app the link opens nothing,
 * so the address itself is always visible. Colors follow the surrounding text (alerts, footers).
 */
export function SupportContact({ topic = 'Müraciət', reference, lead = 'Problem təkrarlansa, bizə yazın:', className }: Props) {
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
      <button type="button" onClick={copy} aria-label={copied ? 'Email ünvanı kopyalandı' : 'Email ünvanını kopyala'}
        className="inline-flex items-center gap-1 whitespace-nowrap rounded px-1 align-middle text-xs font-medium opacity-75 transition-opacity hover:opacity-100">
        {copied ? <Check className="size-3.5" aria-hidden /> : <Copy className="size-3.5" aria-hidden />}
        <span aria-live="polite">{copied ? 'Kopyalandı' : 'Kopyala'}</span>
      </button>
    </p>
  )
}
