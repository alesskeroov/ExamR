import { Alert } from '../../components/common/Alert'
import { EmptyState } from '../../components/common/EmptyState'
import { cn } from '../../lib/utils'
import { useBattleRank, useRankLeaderboard, type BattleRank } from './rank-api'
import { RankEmblem } from './RankEmblem'
import { useTranslation } from 'react-i18next'

const tones: Record<string, string> = { Bronze: 'bg-amber-50 text-amber-800', Silver: 'bg-slate-100 text-slate-600', Gold: 'bg-amber-100 text-amber-700', Platinum: 'bg-sky-50 text-sky-700', Diamond: 'bg-indigo-50 text-indigo-600', Master: 'bg-rose-50 text-rose-700' }
export function RankBadge({ rank, compact = false }: { rank: BattleRank; compact?: boolean }) {
  const { t } = useTranslation()
  return <span className={cn('inline-flex items-center gap-1.5 rounded-lg px-2 py-1 text-xs font-semibold', tones[rank.name])}><RankEmblem name={rank.name} size={18} className="shrink-0" />{t(`battle.rank.${rank.name}`, { defaultValue: rank.name })}{!compact && <span className="tabular-nums">· {rank.rating}</span>}</span>
}
export function RankSummary() {
  const { t } = useTranslation()
  const rank = useBattleRank()
  if (rank.isPending) return <p role="status" className="animate-pulse text-sm text-slate-500">{t('battle.rankSummary.loading')}</p>
  if (rank.isError) return <Alert title={t('battle.rankSummary.loadFailed')}>{rank.error.message}</Alert>
  return <section className="rounded-xl border border-slate-200 bg-white p-5">
    <div className="flex flex-wrap items-center justify-between gap-3"><div><p className="mb-2 text-xs text-slate-500">{t('profile.activity.battleRating')}</p><RankBadge rank={rank.data} /></div><p className="text-sm text-slate-500">{t('battle.rankSummary.highest')} <span className="font-semibold tabular-nums">{rank.data.highestRating}</span></p></div>
    <div role="progressbar" aria-label={t('battle.rankSummary.progress')} aria-valuenow={Math.round(rank.data.progress)} aria-valuemin={0} aria-valuemax={100} className="mt-4 h-1.5 overflow-hidden rounded-full bg-slate-100"><div className="h-full rounded-full bg-indigo-500 motion-safe:transition-[width] motion-safe:duration-200" style={{ width: `${rank.data.progress}%` }} /></div>
    <p className="mt-2 text-xs text-slate-500">{rank.data.nextRank ? t('battle.rankSummary.remaining', { rank: t(`battle.rank.${rank.data.nextRank}`, { defaultValue: rank.data.nextRank }), count: rank.data.remaining }) : t('battle.rankSummary.top')}</p>
    <p className="mt-3 text-xs text-slate-500">{t('battle.rankSummary.record', { total: rank.data.totalRankedBattles, wins: rank.data.wins, losses: rank.data.losses, draws: rank.data.draws })}</p>
    {!rank.data.totalRankedBattles && <p className="mt-2 text-xs text-slate-500">{t('battle.rankSummary.initial')}</p>}
  </section>
}
export function RankLeaderboard() {
  const { t } = useTranslation()
  const leaders = useRankLeaderboard()
  return <section className="mt-5 rounded-xl border border-slate-200 bg-white p-5"><h2 className="font-semibold">{t('battle.rankSummary.leaderboard')}</h2>
    {leaders.isPending && <p role="status" className="mt-3 text-sm text-slate-500">{t('battle.rankSummary.boardLoading')}</p>}
    {leaders.isError && <Alert title={t('battle.rankSummary.boardFailed')}>{leaders.error.message}</Alert>}
    {leaders.data?.length === 0 && <div className="mt-4"><EmptyState title={t('battle.rankSummary.boardEmpty')} description={t('battle.rankSummary.boardEmptyText')} /></div>}
    <ol className="mt-3 divide-y divide-slate-200">{leaders.data?.map((leader, index) => <li key={leader.userId} className="flex flex-wrap items-center gap-3 py-3 text-sm"><span className="w-6 text-slate-500">{index + 1}</span><span className="min-w-0 flex-1 break-words font-medium">{leader.nickname}</span><RankBadge rank={leader.rank} /></li>)}</ol>
  </section>
}
