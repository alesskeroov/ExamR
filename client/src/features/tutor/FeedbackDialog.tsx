import { Bug, CheckCircle2, Lightbulb, Loader2, Send } from 'lucide-react'
import { useId, useRef, useState, type FormEvent } from 'react'
import { Alert } from '../../components/common/Alert'
import { ApiError } from '../../lib/api-client'
import { cn } from '../../lib/utils'
import { useSendFeedback } from './api'
import { GuideDialog } from './GuideDialog'

const maxText = 1500
type Kind = 'problem' | 'idea'
const kinds: { id: Kind; label: string; icon: typeof Bug; hint: string }[] = [
  { id: 'problem', label: 'Problem bildir', icon: Bug, hint: 'Məsələn: kartın cavabı səhvdir, düymə işləmir, sənəd oxunmadı.' },
  { id: 'idea', label: 'Təklif ver', icon: Lightbulb, hint: 'Məsələn: hansı imkan faydalı olardı, nəyi daha rahat etmək olar.' },
]

interface Props {
  onClose: () => void
  documentName?: string
}

export function FeedbackDialog({ onClose, documentName }: Props) {
  const [kind, setKind] = useState<Kind>('problem')
  const [text, setText] = useState('')
  const textId = useId()
  const feedback = useSendFeedback()
  const inFlight = useRef(false)
  const lastRequest = useRef<{ content: string; id: string } | null>(null)
  const hint = kinds.find(k => k.id === kind)!.hint
  const errorMessage = feedback.error instanceof ApiError
    ? feedback.error.problem?.detail || Object.values(feedback.error.problem?.errors ?? {}).flat().join(' ') || feedback.error.message
    : 'Bağlantı alınmadı. Mətniniz bu pəncərədə saxlanılıb, yenidən cəhd edin.'

  const submit = async (event: FormEvent) => {
    event.preventDefault()
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

  return (
    <GuideDialog title="Təklif və iradlar" onClose={onClose}>
      {feedback.isSuccess ? (
        <div className="px-5 py-8 text-center">
          <CheckCircle2 className="mx-auto size-10 text-emerald-600" aria-hidden />
          <h3 role="status" className="mt-4 text-lg font-semibold">Müraciətiniz qəbul edildi</h3>
          <p className="mt-2 text-sm leading-relaxed text-slate-600">Fikrinizi bölüşdüyünüz üçün təşəkkür edirik. Müraciətiniz ExamR komandasına çatdırılacaq.</p>
          <button type="button" onClick={onClose} className="btn-primary mt-6 h-11 rounded-xl px-6 text-sm font-semibold">Bağla</button>
        </div>
      ) : (
        <form onSubmit={submit} className="guide-scroll flex min-h-0 flex-col gap-4 overflow-y-auto px-4 py-4 sm:px-5" aria-busy={feedback.isPending}>
          <p className="text-sm leading-relaxed text-slate-600">Problem gördünüz və ya fikriniz var? Burada yazıb göndərin — müraciətiniz birbaşa ExamR komandasına çatdırılacaq.</p>
          <div role="radiogroup" aria-label="Müraciətin növü" className="grid grid-cols-2 gap-2">
            {kinds.map(({ id, label, icon: Icon }) => (
              <button key={id} type="button" role="radio" aria-checked={kind === id} disabled={feedback.isPending} onClick={() => setKind(id)}
                className={cn('tutor-border flex h-12 items-center justify-center gap-2 rounded-xl border text-sm font-semibold transition-colors',
                  kind === id ? 'border-indigo-400 bg-indigo-50 text-indigo-700' : 'bg-white text-slate-700 hover:bg-slate-50')}>
                <Icon className="size-4" aria-hidden />{label}
              </button>
            ))}
          </div>
          <div className="flex flex-col gap-1.5">
            <label htmlFor={textId} className="text-sm font-semibold text-slate-700">Qısa təsvir</label>
            <textarea id={textId} value={text} onChange={event => setText(event.target.value)} disabled={feedback.isPending}
              required minLength={5} maxLength={maxText} rows={4} placeholder={hint} aria-describedby={`${textId}-count`}
              className="tutor-border min-h-24 resize-y rounded-xl border bg-white px-3 py-2 text-base text-slate-900 outline-none focus:border-indigo-400 sm:text-sm" />
            <p id={`${textId}-count`} className="text-right text-xs text-slate-500">{text.length}/{maxText}</p>
          </div>
          {feedback.isError && <Alert title="Göndərmək mümkün olmadı">{errorMessage}</Alert>}
          <button type="submit" disabled={feedback.isPending || text.trim().length < 5}
            className="btn-primary inline-flex h-11 shrink-0 items-center justify-center gap-2 rounded-xl px-5 text-sm font-semibold disabled:cursor-not-allowed disabled:opacity-50">
            {feedback.isPending ? <Loader2 className="size-4 animate-spin" aria-hidden /> : <Send className="size-4" aria-hidden />}
            {feedback.isPending ? 'Göndərilir...' : 'Göndər'}
          </button>
        </form>
      )}
    </GuideDialog>
  )
}
