import { Bug, Check, Info, Lightbulb, Loader2, Send } from 'lucide-react'
import { useEffect, useId, useRef, useState, type FormEvent, type KeyboardEvent } from 'react'
import { Alert } from '../../components/common/Alert'
import { SupportContact } from '../../components/common/SupportContact'
import { ApiError } from '../../lib/api-client'
import { cn } from '../../lib/utils'
import { useSendFeedback } from './api'
import { GuideDialog } from './GuideDialog'
import { useTranslation } from 'react-i18next'

const maxText = 1500
const autoCloseSeconds = 3
export type FeedbackKind = 'problem' | 'idea'
type Kind = FeedbackKind
const kinds: { id: Kind; icon: typeof Bug }[] = [{ id: 'problem', icon: Bug }, { id: 'idea', icon: Lightbulb }]

interface Props {
  onClose: () => void
  documentName?: string
  initialKind?: FeedbackKind
}

export function FeedbackDialog({ onClose, documentName, initialKind = 'problem' }: Props) {
  const { t } = useTranslation()
  const [kind, setKind] = useState<Kind>(initialKind)
  const [text, setText] = useState('')
  const textId = useId()
  const feedback = useSendFeedback()
  const inFlight = useRef(false)
  const lastRequest = useRef<{ content: string; id: string } | null>(null)
  const textarea = useRef<HTMLTextAreaElement>(null)
  const hint = t(`feedback.hint.${kind}`)
  const canSend = !feedback.isPending && text.trim().length >= 5
  const errorMessage = feedback.error instanceof ApiError
    ? feedback.error.problem?.detail || Object.values(feedback.error.problem?.errors ?? {}).flat().join(' ') || feedback.error.message
    : t('feedback.network')

  const submit = async (event?: FormEvent) => {
    event?.preventDefault()
    if (inFlight.current || text.trim().length < 5) return
    const payload = { kind, text: text.trim(), documentName, pagePath: window.location.pathname }
    const content = JSON.stringify(payload)
    // Reuse the key after a lost response; a changed message is a new submission.
    if (lastRequest.current?.content !== content) lastRequest.current = { content, id: crypto.randomUUID() }
    inFlight.current = true
    try { await feedback.mutateAsync({ ...payload, requestId: lastRequest.current.id }) }
    catch { /* The mutation error is displayed below; retain the text for another attempt. */ }
    finally { inFlight.current = false }
  }

  const onKeyDown = (event: KeyboardEvent<HTMLTextAreaElement>) => {
    if (event.key === 'Enter' && (event.ctrlKey || event.metaKey)) { event.preventDefault(); void submit() }
  }

  const writeAnother = () => {
    feedback.reset()
    lastRequest.current = null
    setText('')
    // The form mounts again on the next render; focus its text field.
    requestAnimationFrame(() => textarea.current?.focus())
  }

  return (
    <GuideDialog title={t('feedback.title')} subtitle={t('feedback.lead')} headerless={feedback.isSuccess} onClose={onClose}>
      {feedback.isSuccess ? <FeedbackSent onClose={onClose} onWriteAnother={writeAnother} /> : (
        <form onSubmit={submit} className="guide-scroll flex min-h-0 flex-col overflow-y-auto" aria-busy={feedback.isPending}>
          <div className="flex flex-col gap-4 px-4 pt-4 pb-5 sm:px-5">
            <div role="radiogroup" aria-label={t('feedback.kindAria')} className="grid grid-cols-2 gap-1 rounded-xl border border-line bg-sunken p-1">
              {kinds.map(({ id, icon: Icon }) => (
                <button key={id} type="button" role="radio" aria-checked={kind === id} disabled={feedback.isPending} onClick={() => setKind(id)}
                  className={cn('flex h-11 items-center justify-center gap-2 rounded-lg border text-sm font-semibold transition-colors',
                    kind === id ? 'border-accent/40 bg-accent-soft text-accent' : 'border-transparent text-muted hover:text-ink')}>
                  <Icon className="size-4" aria-hidden />{id === 'problem' ? t('help.problem') : t('help.idea')}
                </button>
              ))}
            </div>
            <div className="flex flex-col gap-2">
              <label htmlFor={textId} className="text-sm font-semibold text-ink">{t(`feedback.label.${kind}`)}</label>
              <textarea ref={textarea} id={textId} value={text} onChange={event => setText(event.target.value)} onKeyDown={onKeyDown} disabled={feedback.isPending}
                required minLength={5} maxLength={maxText} rows={5} placeholder={hint} aria-describedby={`${textId}-info`}
                className="min-h-32 resize-y rounded-xl border border-line bg-sunken px-4 py-3 text-base text-ink outline-none transition-[border-color,box-shadow] focus:border-accent focus:ring-3 focus:ring-accent/15 sm:text-sm" />
              {text.length > maxText - 200 && <p className="text-right text-xs text-muted" aria-live="polite">{text.length}/{maxText}</p>}
            </div>
            <p id={`${textId}-info`} className="flex items-center gap-2 text-xs text-muted">
              <Info className="size-4 shrink-0" aria-hidden />{t('feedback.autoInfo')}
            </p>
            {feedback.isError && <Alert title={t('feedback.failed')}>{errorMessage}</Alert>}
            <div className="flex items-center justify-between gap-3">
              <p className="hidden items-center gap-1.5 text-xs text-muted sm:flex">
                <kbd className="rounded-md border border-line px-1.5 py-0.5 font-sans text-xs">Ctrl</kbd>+
                <kbd className="rounded-md border border-line px-1.5 py-0.5 font-sans text-xs">Enter</kbd>{t('feedback.shortcut')}
              </p>
              <button type="submit" disabled={!canSend}
                className="inline-flex h-11 w-full shrink-0 items-center justify-center gap-2 rounded-xl bg-accent px-6 text-sm font-semibold text-on-ink transition-[filter,opacity] hover:brightness-110 disabled:cursor-not-allowed disabled:opacity-45 disabled:hover:brightness-100 sm:w-auto">
                {feedback.isPending ? <Loader2 className="size-4 animate-spin" aria-hidden /> : <Send className="size-4" aria-hidden />}
                {feedback.isPending ? t('feedback.sending') : t('battle.chat.send')}
              </button>
            </div>
          </div>
          <SupportContact className="border-t border-line px-4 py-4 text-center text-muted sm:px-5 [&_a]:text-accent"
            topic={t('common.support.topic')} lead={t('feedback.emailLead')} />
        </form>
      )}
    </GuideDialog>
  )
}

function FeedbackSent({ onClose, onWriteAnother }: { onClose: () => void; onWriteAnother: () => void }) {
  const { t } = useTranslation()
  const [seconds, setSeconds] = useState(autoCloseSeconds)
  const closeButton = useRef<HTMLButtonElement>(null)
  // Parents pass an inline onClose; a ref keeps the countdown from restarting when they re-render.
  const close = useRef(onClose)
  useEffect(() => { close.current = onClose })
  useEffect(() => { closeButton.current?.focus() }, [])
  useEffect(() => {
    if (seconds <= 0) { close.current(); return }
    const timer = setTimeout(() => setSeconds(value => value - 1), 1000)
    return () => clearTimeout(timer)
  }, [seconds])

  return (
    <div className="px-6 py-10 text-center sm:px-10">
      <div className="mx-auto grid size-16 place-items-center rounded-full border border-accent/40 bg-accent-soft text-accent">
        <Check className="size-7" strokeWidth={2.5} aria-hidden />
      </div>
      <h3 role="status" className="mt-6 text-2xl font-bold tracking-tight">{t('feedback.accepted')}</h3>
      <p className="mx-auto mt-3 max-w-sm text-sm leading-relaxed text-muted sm:text-base">{t('feedback.thanks')}</p>
      <div className="mt-7 flex justify-center gap-3">
        <button type="button" onClick={onWriteAnother}
          className="h-11 rounded-xl border border-line px-5 text-sm font-semibold text-ink transition-colors hover:bg-sunken">{t('feedback.writeAnother')}</button>
        <button ref={closeButton} type="button" onClick={onClose}
          className="h-11 rounded-xl bg-accent px-6 text-sm font-semibold text-on-ink transition-[filter] hover:brightness-110">{t('common.close')}</button>
      </div>
      <p className="mt-5 text-xs text-muted">{t('feedback.autoClose', { count: seconds })}</p>
    </div>
  )
}
