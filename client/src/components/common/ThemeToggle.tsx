import { Moon, Sun } from 'lucide-react'
import { useTheme } from '../../lib/use-theme'
import { useTranslation } from 'react-i18next'

export function ThemeToggle() {
  const { dark, toggleTheme } = useTheme()
  const { t } = useTranslation()
  return <button type="button" onClick={toggleTheme} aria-pressed={dark}
    aria-label={dark ? t('common.theme.toLight') : t('common.theme.toDark')}
    title={dark ? t('common.theme.toLight') : t('common.theme.toDark')}
    className="app-theme-toggle grid size-10 shrink-0 place-items-center rounded-xl border border-slate-200 bg-white text-slate-600 transition-colors hover:bg-slate-100">
    {dark ? <Sun className="size-[18px]" aria-hidden /> : <Moon className="size-[18px]" aria-hidden />}
  </button>
}
