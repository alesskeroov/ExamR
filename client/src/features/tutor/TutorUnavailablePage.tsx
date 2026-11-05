import { Link } from 'react-router'
import { MessagesSquare } from 'lucide-react'
import { EmptyState } from '../../components/common/EmptyState'

/** Shown instead of the AI Tutor while the feature is not released. */
export function TutorUnavailablePage() {
  return (
    <div className="mx-auto max-w-2xl py-6">
      <EmptyState
        icon={<MessagesSquare className="size-10" aria-hidden />}
        title="AI Tutor hazırda inkişaf mərhələsindədir"
        description="Bu bölmə hələ aktiv deyil. Üzərində işləyirik və hazır olan kimi istifadəyə açacağıq. Hələlik sual bankı və imtahanlarla hazırlaşmağa davam edə bilərsən."
        action={
          <Link to="/exams/new" className="btn-primary inline-flex h-11 items-center rounded-[10px] px-4 text-sm font-semibold">
            İmtahana başla
          </Link>
        }
      />
    </div>
  )
}
