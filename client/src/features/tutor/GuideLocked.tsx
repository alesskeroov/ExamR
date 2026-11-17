import { Crown } from 'lucide-react'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { EmptyState } from '../../components/common/EmptyState'
import { useUsageLimit } from '../usage/api'

/** The study guide is a package feature: packages with a guide limit of 0 (Free, Standart) see an upgrade screen instead. */
export function GuideGate({ children }: { children: ReactNode }) {
  const { t } = useTranslation()
  const { item } = useUsageLimit('Guide')
  if (item?.limit !== 0) return children
  return <div className="mx-auto max-w-2xl py-6">
    <EmptyState
      icon={<Crown className="size-10" aria-hidden />}
      title={t('tutor.locked.title')}
      description={t('tutor.locked.text')}
      action={<Link to="/plans" className="btn-primary inline-flex h-11 items-center rounded-[10px] px-4 text-sm font-semibold">{t('tutor.locked.cta')}</Link>}
    />
  </div>
}
