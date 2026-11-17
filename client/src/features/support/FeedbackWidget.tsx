import { Bug, Check, Lightbulb, Loader2, MessageSquare, SendHorizontal, X } from 'lucide-react'
import { useEffect, useId, useRef, useState, type FormEvent, type KeyboardEvent as ReactKeyboardEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { ApiError } from '../../lib/api-client'
import { supportMailto } from '../../lib/support'
import { cn } from '../../lib/utils'
import { useSendFeedback } from '../tutor/api'
import { onOpenFeedback, type FeedbackKind } from './feedback-events'
import './feedback-widget.css'

const maxText = 1500
const autoCloseSeconds = 3
const kinds: { id: FeedbackKind; icon: typeof Bug }[] = [{ id: 'problem', icon: Bug }, { id: 'idea', icon: Lightbulb }]

/**
 * Floating feedback button (bottom right) with a compact panel: pick problem/idea, write, send.
 * The draft survives closing the panel; page path (and the guide's document) is attached automatically.
 */
export function FeedbackWidget() {
  const { t } = useTranslation()
  const [open, setOpen] = useState(false)
  const [kind, setKind] = useState<FeedbackKind>('problem')
  const [text, setText] = useState('')
  const [documentName, setDocumentName] = useState<string>()
  const feedback = useSendFeedback()
  const inFlight = useRef(false)
  const lastRequest = useRef<{ content: string; id: string } | null>(null)
  const root = useRef<HTMLDivElement>(null)
  const fab = useRef<HTMLButtonElement>(null)
  const textarea = useRef<HTMLTextAreaElement>(null)
  const panelId = useId()
  const canSend = !feedback.isPending && text.trim().length >= 5
  const errorMessage = feedback.error instanceof ApiError
    ? feedback.error.problem?.detail || Object.values(feedback.error.problem?.errors ?? {}).flat().join(' ') || feedback.error.message
    : t('feedback.network')

  const close = (focusButton = true) => {
    setOpen(false)
    // After a successful send the next opening starts with an empty form.
    if (feedback.isSuccess) { feedback.reset(); lastRequest.current = null; setText(''); setDocumentName(undefined) }
    if (focusButton) fab.current?.focus()
  }
  // Event handlers registered in effects call the latest close without re-subscribing.
  const closeRef = useRef(close)
  useEffect(() => { closeRef.current = close })

  useEffect(() => onOpenFeedback(detail => {
    if (detail.kind) setKind(detail.kind)
    setDocumentName(detail.documentName)
    setOpen(true)
  }), [])

  useEffect(() => {
    if (!open) return
    requestAnimationFrame(() => textarea.current?.focus({ preventScroll: true }))
    const onPointer = (event: PointerEvent) => { if (!root.current?.contains(event.target as Node)) closeRef.current(false) }
    const onKey = (event: KeyboardEvent) => {
      if (event.key !== 'Escape') return
      event.stopPropagation()
      closeRef.current()
    }
    document.addEventListener('pointerdown', onPointer)
    document.addEventListener('keydown', onKey, true)
    return () => { document.removeEventListener('pointerdown', onPointer); document.removeEventListener('keydown', onKey, true) }
  }, [open])

  const submit = async (event?: FormEvent) => {
    event?.preventDefault()
    if (inFlight.current || text.trim().length < 5) return
    const payload = { kind, text: text.trim(), documentName, pagePath: window.location.pathname }
    const content = JSON.stringify(payload)
    // Reuse the key after a lost response; a changed message is a new submission.
    if (lastRequest.current?.content !== content) lastRequest.current = { content, id: crypto.randomUUID() }
    inFlight.current = true
    try { await feedback.mutateAsync({ ...payload, requestId: lastRequest.current.id }) }
    catch { /* The mutation error is displayed in the panel; the text stays for another attempt. */ }
    finally { inFlight.current = false }
  }

  const onKeyDown = (event: ReactKeyboardEvent<HTMLTextAreaElement>) => {
    if (event.key === 'Enter' && (event.ctrlKey || event.metaKey)) { event.preventDefault(); void submit() }
  }

  const writeAnother = () => {
    feedback.reset()
    lastRequest.current = null
    setText('')
    requestAnimationFrame(() => textarea.current?.focus())
  }

  return (
    <div ref={root} className="feedback-root">
      {open && (
        <div id={panelId} role="dialog" aria-labelledby={`${panelId}-title`} className="feedback-panel">
          {feedback.isSuccess ? <Sent titleId={`${panelId}-title`} onClose={() => close()} onWriteAnother={writeAnother} /> : (
            <form onSubmit={submit} aria-busy={feedback.isPending}>
              <div className="flex items-center gap-2">
                <h2 id={`${panelId}-title`} className="min-w-0 flex-1 text-[17px] font-bold tracking-tight text-ink">{t('feedback.widgetTitle')}</h2>
                <button type="button" onClick={() => close()} aria-label={t('common.close')}
                  className="-mr-1.5 grid size-9 shrink-0 place-items-center rounded-full text-muted transition-colors hover:bg-sunken hover:text-ink">
                  <X className="size-5" aria-hidden />
                </button>
              </div>

              <div role="radiogroup" aria-label={t('feedback.kindAria')} className="mt-3 flex flex-wrap gap-2">
                {kinds.map(({ id, icon: Icon }) => (
                  <button key={id} type="button" role="radio" aria-checked={kind === id} disabled={feedback.isPending} onClick={() => setKind(id)}
                    className={cn('feedback-chip inline-flex h-10 items-center gap-2 rounded-full border px-4 text-sm font-semibold transition-colors',
                      kind === id ? 'border-accent/50 bg-accent-soft text-accent' : 'border-line text-muted hover:text-ink')}>
                    <Icon className="size-4" aria-hidden />{id === 'problem' ? t('feedback.kindProblem') : t('feedback.kindIdea')}
                  </button>
                ))}
              </div>

              <div className="feedback-box mt-4 rounded-2xl border border-line bg-sunken">
                <label htmlFor={`${panelId}-text`} className="sr-only">{t(`feedback.label.${kind}`)}</label>
                <textarea ref={textarea} id={`${panelId}-text`} value={text} onChange={event => setText(event.target.value)} onKeyDown={onKeyDown}
                  disabled={feedback.isPending} required minLength={5} maxLength={maxText} rows={4}
                  placeholder={kind === 'problem' ? t('feedback.placeholderProblem') : t('feedback.placeholderIdea')}
                  className="block max-h-56 min-h-28 w-full resize-none bg-transparent px-4 pt-3.5 pb-1 text-base leading-relaxed text-ink outline-none placeholder:text-faint sm:text-[15px]" />
                <div className="flex items-center justify-end gap-3 px-2 pb-2">
                  {text.length > maxText - 200 && <span className="text-xs text-muted" aria-live="polite">{text.length}/{maxText}</span>}
                  <button type="submit" disabled={!canSend} aria-label={feedback.isPending ? t('feedback.sending') : t('battle.chat.send')}
                    title={`${t('battle.chat.send')} (Ctrl+Enter)`}
                    className="grid size-11 shrink-0 place-items-center rounded-xl bg-accent text-on-ink transition-[filter,opacity] hover:brightness-110 disabled:cursor-not-allowed disabled:opacity-40 disabled:hover:brightness-100">
                    {feedback.isPending ? <Loader2 className="size-5 animate-spin" aria-hidden /> : <SendHorizontal className="size-5" aria-hidden />}
                  </button>
                </div>
              </div>

              {feedback.isError && (
                <p role="alert" className="mt-3 rounded-xl border border-bad/30 bg-bad/10 px-3 py-2 text-sm text-bad">
                  <span className="font-semibold">{t('feedback.failed')}.</span> {errorMessage}
                </p>
              )}

              <p className="mt-3 text-[13px] leading-5 text-muted">
                {t('feedback.pageInfo')} ·{' '}
                <a href={supportMailto({ topic: t('common.support.topic') })} className="font-medium whitespace-nowrap text-accent underline underline-offset-2 hover:brightness-110">
                  {t('feedback.emailLink')}
                </a>
              </p>
            </form>
          )}
        </div>
      )}

      <button ref={fab} type="button" onClick={() => (open ? close() : setOpen(true))} aria-expanded={open} aria-controls={open ? panelId : undefined}
        aria-label={open ? t('common.close') : t('feedback.open')} title={t('feedback.open')} className="feedback-fab">
        {open ? <X className="size-6" aria-hidden /> : <MessageSquare className="size-6" aria-hidden />}
      </button>
    </div>
  )
}

function Sent({ titleId, onClose, onWriteAnother }: { titleId: string; onClose: () => void; onWriteAnother: () => void }) {
  const { t } = useTranslation()
  const [seconds, setSeconds] = useState(autoCloseSeconds)
  const close = useRef(onClose)
  useEffect(() => { close.current = onClose })
  useEffect(() => {
    if (seconds <= 0) { close.current(); return }
    const timer = setTimeout(() => setSeconds(value => value - 1), 1000)
    return () => clearTimeout(timer)
  }, [seconds])

  return (
    <div className="py-3 text-center">
      <div className="mx-auto grid size-12 place-items-center rounded-full border border-accent/40 bg-accent-soft text-accent">
        <Check className="size-6" strokeWidth={2.5} aria-hidden />
      </div>
      <h2 id={titleId} role="status" className="mt-4 text-lg font-bold tracking-tight text-ink">{t('feedback.accepted')}</h2>
      <p className="mx-auto mt-1.5 max-w-xs text-sm leading-relaxed text-muted">{t('feedback.thanks')}</p>
      <div className="mt-5 flex justify-center gap-2">
        <button type="button" onClick={onWriteAnother}
          className="h-10 rounded-xl border border-line px-4 text-sm font-semibold text-ink transition-colors hover:bg-sunken">{t('feedback.writeAnother')}</button>
        <button type="button" onClick={onClose}
          className="h-10 rounded-xl bg-accent px-5 text-sm font-semibold text-on-ink transition-[filter] hover:brightness-110">{t('common.close')}</button>
      </div>
      <p className="mt-3 text-xs text-muted">{t('feedback.autoClose', { count: seconds })}</p>
    </div>
  )
}
