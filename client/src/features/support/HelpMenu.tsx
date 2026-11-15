import { Bug, Check, CircleHelp, Copy, Lightbulb, Mail, X, type LucideIcon } from 'lucide-react'
import { useEffect, useId, useRef, useState, type CSSProperties } from 'react'
import { SUPPORT_EMAIL, supportMailto } from '../../lib/support'
import { cn } from '../../lib/utils'
import { FeedbackDialog, type FeedbackKind } from '../tutor/FeedbackDialog'
import './help-menu.css'

/**
 * "?" button in the top bar, next to the theme toggle and the bell — where Google, GitHub and Atlassian keep help.
 * It opens a short panel: report a problem, suggest an idea (both the in-app form) or write an email.
 */
export function HelpMenu() {
  const [open, setOpen] = useState(false)
  const [form, setForm] = useState<FeedbackKind | null>(null)
  const [copied, setCopied] = useState(false)
  const [sheetTop, setSheetTop] = useState<number | null>(null)
  const root = useRef<HTMLDivElement>(null)
  const button = useRef<HTMLButtonElement>(null)
  const panelId = useId()

  const close = (focusButton = false) => {
    setOpen(false)
    if (focusButton) button.current?.focus()
  }

  useEffect(() => {
    if (!open) return
    const onPointer = (event: PointerEvent) => { if (!root.current?.contains(event.target as Node)) setOpen(false) }
    const onKey = (event: KeyboardEvent) => {
      if (event.key !== 'Escape') return
      event.stopPropagation()
      close(true)
    }
    document.addEventListener('pointerdown', onPointer)
    document.addEventListener('keydown', onKey, true)
    return () => { document.removeEventListener('pointerdown', onPointer); document.removeEventListener('keydown', onKey, true) }
  }, [open])

  useEffect(() => {
    if (!copied) return
    const timer = setTimeout(() => setCopied(false), 2000)
    return () => clearTimeout(timer)
  }, [copied])

  const toggle = () => {
    const rect = button.current?.getBoundingClientRect()
    setSheetTop(rect ? Math.round(rect.bottom + 8) : null)
    setOpen(value => !value)
  }
  const openForm = (kind: FeedbackKind) => { setOpen(false); setForm(kind) }
  const copy = () => { navigator.clipboard?.writeText(SUPPORT_EMAIL).then(() => setCopied(true), () => { /* address is visible */ }) }

  return (
    <div ref={root} className="help-root relative shrink-0">
      <button ref={button} type="button" onClick={toggle} aria-expanded={open} aria-controls={panelId} aria-haspopup="dialog"
        aria-label="Kömək və əlaqə" title="Kömək və əlaqə"
        className={cn('grid size-10 place-items-center rounded-lg text-slate-600 transition-colors duration-150 hover:bg-slate-100 hover:text-slate-900', open && 'bg-slate-100 text-slate-900')}>
        <CircleHelp className="size-5" aria-hidden />
      </button>

      {open && (
        <div id={panelId} role="dialog" aria-labelledby={`${panelId}-title`} className="help-panel"
          style={{ '--help-sheet-top': sheetTop ? `${sheetTop}px` : undefined } as CSSProperties}>
          <div className="flex items-start gap-2 px-4 pt-3.5 pb-2">
            <div className="min-w-0 flex-1">
              <p id={`${panelId}-title`} className="text-sm font-semibold text-slate-900">Kömək və əlaqə</p>
              <p className="mt-0.5 text-xs leading-5 text-slate-500">Problem yarandı və ya fikriniz var? Bizə bildirin.</p>
            </div>
            <button type="button" onClick={() => close(true)} aria-label="Bağla"
              className="-mt-1 -mr-2 grid size-8 shrink-0 place-items-center rounded-md text-slate-500 hover:bg-slate-100 hover:text-slate-900"><X className="size-4" /></button>
          </div>

          <div className="flex flex-col px-2 pb-2">
            <Item icon={Bug} title="Problem bildir" text="Xəta, işləməyən düymə, səhv sual" onClick={() => openForm('problem')} />
            <Item icon={Lightbulb} title="Təklif ver" text="Nəyi daha rahat edək?" onClick={() => openForm('idea')} />
          </div>

          <div className="flex items-center gap-1 border-t border-slate-200 px-2 py-2">
            <a href={supportMailto({ topic: 'Müraciət' })} onClick={event => { event.currentTarget.href = supportMailto({ topic: 'Müraciət' }) }}
              className="help-item flex min-w-0 flex-1 items-center gap-3 rounded-lg px-2 py-2">
              <span className="help-item-icon grid size-8 shrink-0 place-items-center rounded-lg"><Mail className="size-4" aria-hidden /></span>
              <span className="min-w-0">
                <span className="block text-sm font-medium">Email ilə yaz</span>
                <span className="block truncate text-xs text-slate-500">{SUPPORT_EMAIL}</span>
              </span>
            </a>
            <button type="button" onClick={copy} aria-label={copied ? 'Email ünvanı kopyalandı' : 'Email ünvanını kopyala'} title="Kopyala"
              className="grid size-9 shrink-0 place-items-center rounded-lg text-slate-500 hover:bg-slate-100 hover:text-slate-900">
              {copied ? <Check className="size-4 text-emerald-600" aria-hidden /> : <Copy className="size-4" aria-hidden />}
            </button>
            <span className="sr-only" aria-live="polite">{copied ? 'Kopyalandı' : ''}</span>
          </div>
        </div>
      )}

      {form && <FeedbackDialog key={form} initialKind={form} onClose={() => { setForm(null); button.current?.focus() }} />}
    </div>
  )
}

function Item({ icon: Icon, title, text, onClick }: { icon: LucideIcon; title: string; text: string; onClick: () => void }) {
  return (
    <button type="button" onClick={onClick} className="help-item flex items-center gap-3 rounded-lg px-2 py-2 text-left">
      <span className="help-item-icon grid size-8 shrink-0 place-items-center rounded-lg"><Icon className="size-4" aria-hidden /></span>
      <span className="min-w-0">
        <span className="block text-sm font-medium">{title}</span>
        <span className="block text-xs text-slate-500">{text}</span>
      </span>
    </button>
  )
}
