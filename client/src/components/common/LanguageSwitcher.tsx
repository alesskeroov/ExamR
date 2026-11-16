import { Check, ChevronDown } from 'lucide-react'
import { useEffect, useId, useRef, useState, type CSSProperties } from 'react'
import { useTranslation } from 'react-i18next'
import { currentLanguage, languages, type Language } from '../../lib/i18n'
import { cn } from '../../lib/utils'

// Inside the landing/auth pages the --lp-* tokens win; elsewhere the app tokens apply.
const menuStyle: CSSProperties = {
  background: 'var(--lp-surface, var(--app-surface))',
  borderColor: 'var(--lp-line, var(--app-border))',
  color: 'var(--lp-ink, var(--app-ink))',
}

/** Flag picker for AZ / EN / RU; the choice applies at once (no reload) and is remembered in localStorage. */
export function LanguageSwitcher({ className, bare }: { className?: string; /** Skip the app's border/background (pages with their own button style). */ bare?: boolean }) {
  const { t, i18n } = useTranslation()
  const active = currentLanguage(i18n.resolvedLanguage)
  const [open, setOpen] = useState(false)
  const root = useRef<HTMLDivElement>(null)
  const button = useRef<HTMLButtonElement>(null)
  const menuId = useId()

  useEffect(() => {
    if (!open) return
    const onPointer = (event: PointerEvent) => { if (!root.current?.contains(event.target as Node)) setOpen(false) }
    const onKey = (event: KeyboardEvent) => {
      if (event.key !== 'Escape') return
      event.stopPropagation()
      setOpen(false)
      button.current?.focus()
    }
    document.addEventListener('pointerdown', onPointer)
    document.addEventListener('keydown', onKey, true)
    return () => { document.removeEventListener('pointerdown', onPointer); document.removeEventListener('keydown', onKey, true) }
  }, [open])

  const choose = (code: Language) => {
    setOpen(false)
    button.current?.focus()
    if (code !== active) void i18n.changeLanguage(code)
  }

  return <div ref={root} className="relative shrink-0">
    <button ref={button} type="button" onClick={() => setOpen(value => !value)} aria-haspopup="menu" aria-expanded={open} aria-controls={menuId}
      aria-label={`${t('common.language')}: ${languages.find(l => l.code === active)?.name}`} title={t('common.language')}
      className={cn('inline-flex h-10 items-center gap-1.5 pr-2 pl-2.5 text-[13px] font-semibold focus-visible:outline-2 focus-visible:outline-indigo-500',
        !bare && 'app-theme-toggle rounded-xl border border-slate-200 bg-white text-slate-600 transition-colors hover:bg-slate-100', className)}>
      <Flag code={active} />
      <span>{languages.find(l => l.code === active)?.label}</span>
      <ChevronDown className={cn('size-3.5 transition-transform', open && 'rotate-180')} aria-hidden />
    </button>
    {open && (
      <ul id={menuId} role="menu" aria-label={t('common.language')} style={menuStyle}
        className="absolute top-full right-0 z-50 mt-2 min-w-48 rounded-xl border p-1 shadow-lg">
        {languages.map(l => (
          <li key={l.code} role="none">
            <button type="button" role="menuitemradio" aria-checked={l.code === active} lang={l.code} onClick={() => choose(l.code)}
              className="flex w-full items-center gap-2.5 rounded-lg px-2.5 py-2 text-left text-sm hover:bg-[var(--lp-sunken,var(--app-sunken))] focus-visible:bg-[var(--lp-sunken,var(--app-sunken))] focus-visible:outline-none">
              <Flag code={l.code} />
              <span className={cn('flex-1', l.code === active && 'font-semibold')}>{l.name}</span>
              {l.code === active && <Check className="size-4 shrink-0" aria-hidden />}
            </button>
          </li>
        ))}
      </ul>
    )}
  </div>
}

/** All three flags side by side as radio buttons, for menus with room to show every choice (landing mobile sheet). */
export function LanguageRadios({ className }: { className?: string }) {
  const { t, i18n } = useTranslation()
  const active = currentLanguage(i18n.resolvedLanguage)
  return <div className={className} role="radiogroup" aria-label={t('common.language')}>
    {languages.map(l => (
      <button key={l.code} type="button" role="radio" aria-checked={l.code === active} aria-label={l.name} lang={l.code}
        onClick={() => { if (l.code !== active) void i18n.changeLanguage(l.code) }}>
        <Flag code={l.code} />{l.label}
      </button>
    ))}
  </div>
}

/** Small inline SVG flags: emoji flags do not render on Windows. */
function Flag({ code }: { code: Language }) {
  const id = useId().replace(/:/g, '')
  return <span className="inline-flex h-[14px] w-[21px] shrink-0 overflow-hidden rounded-[3px] ring-1 ring-black/10" aria-hidden>
    <svg viewBox="0 0 60 30" preserveAspectRatio="xMidYMid slice" className="size-full">
      {code === 'az' && <>
        <rect width="60" height="10" fill="#00B5E2" />
        <rect y="10" width="60" height="10" fill="#EF3340" />
        <rect y="20" width="60" height="10" fill="#509E2F" />
        <circle cx="28" cy="15" r="4.5" fill="#fff" />
        <circle cx="29.3" cy="15" r="3.6" fill="#EF3340" />
        <polygon fill="#fff" points={starPoints(33.6, 15, 2.1, 0.95)} />
      </>}
      {code === 'en' && <>
        <clipPath id={`${id}-t`}><path d="M30,15 h30 v15 z v15 h-30 z h-30 v-15 z v-15 h30 z" /></clipPath>
        <rect width="60" height="30" fill="#012169" />
        <path d="M0,0 L60,30 M60,0 L0,30" stroke="#fff" strokeWidth="6" />
        <path d="M0,0 L60,30 M60,0 L0,30" clipPath={`url(#${id}-t)`} stroke="#C8102E" strokeWidth="4" />
        <path d="M30,0 v30 M0,15 h60" stroke="#fff" strokeWidth="10" />
        <path d="M30,0 v30 M0,15 h60" stroke="#C8102E" strokeWidth="6" />
      </>}
      {code === 'ru' && <>
        <rect width="60" height="10" fill="#fff" />
        <rect y="10" width="60" height="10" fill="#0039A6" />
        <rect y="20" width="60" height="10" fill="#D52B1E" />
      </>}
    </svg>
  </span>
}

/** Eight-pointed star like the one on the flag of Azerbaijan. */
function starPoints(cx: number, cy: number, outer: number, inner: number) {
  return Array.from({ length: 16 }, (_, i) => {
    const r = i % 2 === 0 ? outer : inner
    const angle = (Math.PI / 8) * i - Math.PI / 2
    return `${(cx + r * Math.cos(angle)).toFixed(2)},${(cy + r * Math.sin(angle)).toFixed(2)}`
  }).join(' ')
}
