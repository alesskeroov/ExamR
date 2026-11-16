import { BadgeCheck } from 'lucide-react'
import { cn } from '../../lib/utils'
import { useTranslation } from 'react-i18next'

/** Green check shown after the name of an account whose email is confirmed (or proven through Google sign-in). */
export function VerifiedBadge({ className }: { className?: string }) {
  const { t } = useTranslation()
  return (
    <span className={cn('inline-flex shrink-0 align-[-0.15em] text-emerald-600', className)} title={t('common.verified')}>
      <BadgeCheck className="size-[1.05em]" aria-hidden />
      <span className="sr-only">{t('common.verified')}</span>
    </span>
  )
}
