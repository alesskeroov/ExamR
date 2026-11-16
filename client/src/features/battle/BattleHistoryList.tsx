import { Medal, Swords } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router'
import { Alert } from '../../components/common/Alert'
import { Button } from '../../components/common/Button'
import { EmptyState } from '../../components/common/EmptyState'
import { formatDate, formatNumber } from '../../lib/format'
import { cn } from '../../lib/utils'
import { sourceName, sourceRange } from '../exams/examSource'
import { useBattleHistory } from './api'
import { useTranslation } from 'react-i18next'

const medal = ['text-amber-500', 'text-slate-400', 'text-orange-400']

/** The signed-in user's own battle results (the API only returns the caller's rows). */
export function BattleHistoryList() {
  const { t } = useTranslation()
  const [page, setPage] = useState(1)
  const history = useBattleHistory(page)
  const totalPages = history.data ? Math.max(1, Math.ceil(history.data.totalCount / history.data.pageSize)) : 1

  if (history.isPending) return <div className="h-48 animate-pulse rounded-xl bg-slate-200/60" aria-label={t('common.loading')} />
  if (history.isError) return <Alert title={t('history.loadFailed')}>{history.error.message}</Alert>
  if (history.data.items.length === 0) {
    return (
      <EmptyState
        icon={<Swords className="size-8" />}
        title={t('battle.history.empty')}
        action={
          <Link to="/battle" className="font-medium text-indigo-600 hover:text-indigo-700">
            {t('battle.history.emptyAction')}
          </Link>
        }
      />
    )
  }

  return (
    <>
      <ul className="flex flex-col gap-2">
        {history.data.items.map((item) => (
          <li key={item.battleId} className="flex items-center gap-4 rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
            <div className="flex w-16 shrink-0 flex-col items-center">
              {item.rank <= 3 ? <Medal className={cn('size-6', medal[item.rank - 1])} aria-hidden /> : null}
              <span className="font-semibold tabular-nums">
                {item.rank}/{item.participantCount}
              </span>
            </div>
            <div className="min-w-0 flex-1">
              <p className="truncate font-medium">{sourceName(item.source)}</p>
              <p className="text-slate-700">
                {sourceRange(item.source) && `${t('history.questionsRange', { range: sourceRange(item.source) })} · `}
                {t('common.questions', { count: item.questionCount })} · {item.correctCount} {t('dashboard.correctWord')}
              </p>
              <p className="text-slate-500">
                {formatDate(item.playedAt)} · {t('battle.history.room')} <span className="font-mono">{item.roomCode}</span>
              </p>
            </div>
            <span className="text-right text-lg font-bold text-indigo-700 tabular-nums">{formatNumber(item.score)}</span>
          </li>
        ))}
      </ul>
      {totalPages > 1 && (
        <div className="mt-6 flex items-center justify-center gap-3">
          <Button variant="secondary" disabled={page === 1} onClick={() => setPage((p) => p - 1)}>
            {t('common.previous')}
          </Button>
          <span className="text-slate-500 tabular-nums">
            {page} / {totalPages}
          </span>
          <Button variant="secondary" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}>
            {t('common.next')}
          </Button>
        </div>
      )}
    </>
  )
}
