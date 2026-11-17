import { X } from 'lucide-react'
import { useEffect, useId, useRef, type ReactNode } from 'react'
import { cn } from '../../lib/utils'
import { useTranslation } from 'react-i18next'

interface Props {
  title: string
  onClose: () => void
  children: ReactNode
  /** sheet: slides up from the bottom on phones, centered on larger screens. full: fills the phone screen (card viewer). */
  variant?: 'sheet' | 'full'
  /** Extra controls in the header, before the close button. */
  actions?: ReactNode
  /** Keeps the height while the content shrinks (a list being searched), so the dialog does not jump. */
  tall?: boolean
  /** A line under the title; the header then drops its divider and uses a larger title. */
  subtitle?: string
  /** No visible header (the content has its own heading and close button); the title stays for screen readers. */
  headerless?: boolean
}

/** Modal built on the native dialog element: focus stays inside, Esc and the backdrop close it. */
export function GuideDialog({ title, onClose, children, variant = 'sheet', actions, tall = false, subtitle, headerless = false }: Props) {
  const { t } = useTranslation()
  const dialog = useRef<HTMLDialogElement>(null)
  const titleId = useId()
  useEffect(() => {
    const element = dialog.current
    element?.showModal()
    // showModal focuses the first control (the close button); a child marked data-autofocus (the card) gets it instead.
    element?.querySelector<HTMLElement>('[data-autofocus]')?.focus({ preventScroll: true })
    // The page behind must not scroll while the dialog is open.
    const overflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    return () => { element?.close(); document.body.style.overflow = overflow }
  }, [])

  return (
    <dialog ref={dialog} aria-labelledby={titleId} onCancel={event => { event.preventDefault(); onClose() }}
      onClick={event => { if (event.target === event.currentTarget) onClose() }}
      className={cn('tutor-surface flex-col overflow-hidden p-0 text-slate-900 shadow-2xl backdrop:bg-slate-950/60 backdrop:backdrop-blur-sm open:flex',
        variant === 'full'
          ? 'm-0 h-dvh max-h-dvh w-full max-w-none rounded-none sm:m-auto sm:h-[min(92dvh,46rem)] sm:max-h-[92dvh] sm:w-[calc(100%-3rem)] sm:max-w-3xl sm:rounded-3xl'
          : 'mx-0 mt-auto mb-0 max-h-[85dvh] w-full max-w-none rounded-t-3xl sm:m-auto sm:w-[calc(100%-3rem)] sm:max-w-xl sm:rounded-3xl',
        tall && variant === 'sheet' && 'h-[85dvh] sm:h-[min(36rem,85dvh)]')}>
      {headerless ? <h2 id={titleId} className="sr-only">{title}</h2> : <header className={cn('flex shrink-0 gap-2 px-4 sm:px-5', subtitle ? 'items-start pt-4 pb-1 sm:pt-5' : 'tutor-border items-center border-b py-2.5')}>
        {subtitle ? (
          <div className="min-w-0 flex-1 pt-1">
            <h2 id={titleId} className="text-xl font-bold tracking-tight">{title}</h2>
            <p className="mt-1 text-sm leading-relaxed text-muted">{subtitle}</p>
          </div>
        ) : <h2 id={titleId} className="min-w-0 flex-1 truncate text-base font-semibold">{title}</h2>}
        {actions}
        <button type="button" onClick={onClose} aria-label={t('common.close')}
          className="grid size-10 shrink-0 place-items-center rounded-full text-slate-500 hover:bg-slate-100 hover:text-slate-900">
          <X className="size-5" />
        </button>
      </header>}
      {children}
    </dialog>
  )
}
