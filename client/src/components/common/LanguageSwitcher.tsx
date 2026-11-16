import { ChevronDown } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { currentLanguage, languages } from '../../lib/i18n'
import { cn } from '../../lib/utils'

/** Compact AZ / EN / RU picker; the choice applies at once (no reload) and is remembered in localStorage. */
export function LanguageSwitcher({ className, bare }: { className?: string; /** Skip the app's border/background (pages with their own button style). */ bare?: boolean }) {
  const { t, i18n } = useTranslation()
  const active = currentLanguage(i18n.resolvedLanguage)
  const label = languages.find(l => l.code === active)?.label
  return <label title={t('common.language')} className={cn('relative inline-flex h-10 shrink-0 cursor-pointer items-center gap-1 pr-2 pl-2.5 text-[13px] font-semibold focus-within:ring-2 focus-within:ring-indigo-500',
    !bare && 'app-theme-toggle rounded-xl border border-slate-200 bg-white text-slate-600 transition-colors hover:bg-slate-100', className)}>
    <span aria-hidden>{label}</span>
    <ChevronDown className="size-3.5" aria-hidden />
    {/* The native select keeps keyboard and screen-reader support; it is invisible over the compact label. */}
    <select value={active} onChange={event => void i18n.changeLanguage(event.target.value)} aria-label={t('common.language')}
      className="absolute inset-0 cursor-pointer opacity-0">
      {languages.map(l => <option key={l.code} value={l.code} lang={l.code}>{l.name}</option>)}
    </select>
  </label>
}
