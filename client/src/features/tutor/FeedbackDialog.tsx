import { Bug, Check, Copy, Lightbulb, Mail } from 'lucide-react'
import { useId, useState } from 'react'
import { cn } from '../../lib/utils'
import { GuideDialog } from './GuideDialog'

const address = 'examr.contact@gmail.com'
const maxText = 1500

type Kind = 'problem' | 'idea'
const kinds: { id: Kind; label: string; icon: typeof Bug; hint: string }[] = [
  { id: 'problem', label: 'Problem bildir', icon: Bug, hint: 'Məsələn: kartın cavabı səhvdir, düymə işləmir, sənəd oxunmadı.' },
  { id: 'idea', label: 'Təklif ver', icon: Lightbulb, hint: 'Məsələn: hansı imkan faydalı olardı, nəyi daha rahat etmək olar.' },
]

interface Props {
  onClose: () => void
  /** The open document, so the team knows where the student was. */
  documentName?: string
}

/** A ready, polite e-mail: the student only adds a sentence or two, the rest is filled in. */
function mailto(kind: Kind, text: string, documentName?: string) {
  const summary = text.trim().split('\n')[0].slice(0, 60)
  const subject = `[ExamR · Öyrənmə bələdçisi] ${kind === 'problem' ? 'Problem' : 'Təklif'}${summary ? `: ${summary}` : ''}`
  const lines = [
    'Salam, ExamR komandası!',
    '',
    kind === 'problem' ? 'Öyrənmə bələdçisində bir problemlə qarşılaşdım:' : 'Öyrənmə bələdçisi ilə bağlı bir təklifim var:',
    text.trim() || '(burada qısa təsvir edin)',
    '',
    ...(kind === 'problem' ? ['Nə etdim:', 'Nə gözləyirdim:', 'Nə baş verdi:', ''] : []),
    ...(documentName ? [`Sənəd: ${documentName}`] : []),
    `Bölmə: ${window.location.pathname}`,
    '',
    'Hörmətlə,',
  ]
  return `mailto:${address}?subject=${encodeURIComponent(subject)}&body=${encodeURIComponent(lines.join('\n'))}`
}

/** "Təklif və iradlar": pick a type, write a line, and the mail app opens with everything else filled in. */
export function FeedbackDialog({ onClose, documentName }: Props) {
  const [kind, setKind] = useState<Kind>('problem')
  const [text, setText] = useState('')
  const [copied, setCopied] = useState(false)
  const textId = useId()
  const hint = kinds.find(k => k.id === kind)!.hint

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(address)
      setCopied(true)
      setTimeout(() => setCopied(false), 2000)
    } catch { /* clipboard blocked: the address stays visible to copy by hand */ }
  }

  return (
    <GuideDialog title="Təklif və iradlar" onClose={onClose}>
      <div className="guide-scroll flex min-h-0 flex-col gap-4 overflow-y-auto px-4 py-4 sm:px-5">
        <p className="text-sm leading-relaxed text-slate-600">
          Öyrənmə bələdçisi demo mərhələsindədir. Problem gördünüz və ya fikriniz var? Bir-iki cümlə yazın — qalanını məktuba biz əlavə edirik.
        </p>

        <div role="radiogroup" aria-label="Müraciətin növü" className="grid grid-cols-2 gap-2">
          {kinds.map(({ id, label, icon: Icon }) => (
            <button key={id} type="button" role="radio" aria-checked={kind === id} onClick={() => setKind(id)}
              className={cn('tutor-border flex h-12 items-center justify-center gap-2 rounded-xl border text-sm font-semibold transition-colors',
                kind === id ? 'border-indigo-400 bg-indigo-50 text-indigo-700' : 'bg-white text-slate-700 hover:bg-slate-50')}>
              <Icon className="size-4" aria-hidden />{label}
            </button>
          ))}
        </div>

        <div className="flex flex-col gap-1.5">
          <label htmlFor={textId} className="text-sm font-semibold text-slate-700">Qısa təsvir <span className="font-normal text-slate-500">(istəyə bağlı)</span></label>
          <textarea id={textId} value={text} onChange={e => setText(e.target.value)} maxLength={maxText} rows={4} placeholder={hint}
            className="tutor-border min-h-24 resize-y rounded-xl border bg-white px-3 py-2 text-base text-slate-900 outline-none focus:border-indigo-400 sm:text-sm" />
        </div>

        <a href={mailto(kind, text, documentName)}
          className="btn-primary inline-flex h-11 items-center justify-center gap-2 rounded-xl px-5 text-sm font-semibold">
          <Mail className="size-4" aria-hidden />E-poçt ilə göndər
        </a>

        <div className="tutor-border flex items-center gap-2 rounded-xl border px-3 py-2">
          <div className="min-w-0 flex-1">
            <p className="text-xs text-slate-500">Poçt proqramı açılmırsa, bu ünvana yazın:</p>
            <p className="truncate text-sm font-semibold text-slate-800 select-all">{address}</p>
          </div>
          <button type="button" onClick={copy} aria-live="polite"
            className="inline-flex h-9 shrink-0 items-center gap-1.5 rounded-lg px-2.5 text-sm font-medium text-indigo-700 hover:bg-indigo-50">
            {copied ? <><Check className="size-4" aria-hidden />Kopyalandı</> : <><Copy className="size-4" aria-hidden />Kopyala</>}
          </button>
        </div>
      </div>
    </GuideDialog>
  )
}
