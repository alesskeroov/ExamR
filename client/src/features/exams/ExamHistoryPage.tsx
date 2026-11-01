import { ChevronRight, History, PlayCircle } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router'
import { Alert } from '../../components/common/Alert'
import { Button } from '../../components/common/Button'
import { EmptyState } from '../../components/common/EmptyState'
import { PageHeader } from '../../components/common/PageHeader'
import { StatusBadge } from '../../components/common/StatusBadge'
import { formatDate, formatNumber } from '../../lib/format'
import { cn } from '../../lib/utils'
import { useExamHistory, type ExamSummary } from './api'
import { sourceName, sourceRange } from './examSource'

export function ExamHistoryPage() {
  const [page, setPage] = useState(1)
  const history = useExamHistory(page)
  const totalPages = history.data ? Math.max(1, Math.ceil(history.data.totalCount / history.data.pageSize)) : 1

  return (
    <>
      <PageHeader
        title="Tarixçə"
        description="Keçmiş imtahanlarınız və nəticələriniz."
        actions={
          <Link to="/exams/new" className="inline-flex h-10 items-center gap-2 rounded-lg bg-indigo-600 px-4 font-medium text-white hover:bg-indigo-700">
            <PlayCircle className="size-4" aria-hidden />
            Yeni imtahan
          </Link>
        }
      />

      {history.isPending ? (
        <div className="h-48 animate-pulse rounded-xl bg-slate-200/60" aria-label="Yüklənir" />
      ) : history.isError ? (
        <Alert title="Tarixçə yüklənmədi">{history.error.message}</Alert>
      ) : history.data.items.length === 0 ? (
        <EmptyState icon={<History className="size-8" />} title="Hələ imtahan yoxdur" description="İlk imtahanınızı başladın — nəticələr burada saxlanılacaq." />
      ) : (
        <>
          <ul className="flex flex-col gap-2">
            {history.data.items.map((exam) => (
              <li key={exam.id}>
                <HistoryRow exam={exam} />
              </li>
            ))}
          </ul>
          {totalPages > 1 && (
            <div className="mt-6 flex items-center justify-center gap-3">
              <Button variant="secondary" disabled={page === 1} onClick={() => setPage((p) => p - 1)}>
                Əvvəlki
              </Button>
              <span className="text-slate-500 tabular-nums">
                {page} / {totalPages}
              </span>
              <Button variant="secondary" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}>
                Növbəti
              </Button>
            </div>
          )}
        </>
      )}
    </>
  )
}

function HistoryRow({ exam }: { exam: ExamSummary }) {
  const inProgress = exam.status === 'InProgress'
  const score = exam.scorePercent
  const scoreTone = score === null ? 'text-slate-400' : score >= 80 ? 'text-emerald-700' : score >= 50 ? 'text-amber-700' : 'text-rose-700'

  return (
    <Link
      to={inProgress ? `/exams/${exam.id}` : `/exams/${exam.id}/result`}
      className="flex items-center gap-4 rounded-xl border border-slate-200 bg-white p-4 shadow-sm transition-colors duration-150 hover:border-slate-300 hover:bg-slate-50"
    >
      <div className={cn('w-16 shrink-0 text-center text-xl font-semibold tabular-nums', scoreTone)}>
        {inProgress ? '…' : score === null ? '—' : `${formatNumber(score)}%`}
      </div>
      <div className="min-w-0 flex-1">
        <p className="truncate font-medium" title={sourceName(exam.source)}>
          {sourceName(exam.source)}
        </p>
        <p className="text-slate-700">
          {sourceRange(exam.source) && `Suallar ${sourceRange(exam.source)} · `}
          {exam.questionCount} sual
        </p>
        <p className="text-slate-500">
          {formatDate(exam.startedAt)} · {exam.revealMode === 'Immediate' ? 'cavablar dərhal' : 'cavablar sonda'}
          {!inProgress && ` · ${exam.correct} düzgün, ${exam.incorrect} yanlış, ${exam.unanswered} cavabsız`}
        </p>
      </div>
      {inProgress ? (
        <StatusBadge tone="info">Davam edir</StatusBadge>
      ) : exam.completionReason === 'TimeExpired' ? (
        <StatusBadge tone="warning">Vaxt bitdi</StatusBadge>
      ) : null}
      <ChevronRight className="size-4 shrink-0 text-slate-400" aria-hidden />
    </Link>
  )
}
