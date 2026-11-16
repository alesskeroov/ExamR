import { Link } from 'react-router'
import { MessagesSquare } from 'lucide-react'
import { EmptyState } from '../../components/common/EmptyState'
import { useTranslation } from 'react-i18next'

/** Shown instead of the study guide while the feature is not released. */
export function TutorUnavailablePage() {
  const { t } = useTranslation()
  return (
    <div className="mx-auto max-w-2xl py-6">
      <EmptyState
        icon={<MessagesSquare className="size-10" aria-hidden />}
        title={t('tutor.unavailable.title')}
        description={t('tutor.unavailable.text')}
        action={
          <Link to="/exams/new" className="btn-primary inline-flex h-11 items-center rounded-[10px] px-4 text-sm font-semibold">
            {t('nav.items.newExam')}
          </Link>
        }
      />
    </div>
  )
}
