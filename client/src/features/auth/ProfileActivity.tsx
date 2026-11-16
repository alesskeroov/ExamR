import { RankLeaderboard, RankSummary } from '../battle/RankSummary'
import { useBattleRank } from '../battle/rank-api'
import { useState } from 'react'
import { Link } from 'react-router'
import { Alert } from '../../components/common/Alert'
import { Button } from '../../components/common/Button'
import { EmptyState } from '../../components/common/EmptyState'
import { formatDate } from '../../lib/format'
import { useAllExams } from '../dashboard/api'
import { BattleInviteModal } from '../friends/BattleInviteModal'
import { useFriends, type Friend } from '../friends/api'
import { useProfileBattles } from './profile-api'
import { useTranslation } from 'react-i18next'

const card = 'rounded-xl border border-slate-200 bg-white p-5'

export function ProfileActivity({ tab }: { tab: number }) {
  // Mount only the active section: the social polling hook stays off other tabs.
  if (tab === 3) return <ProfileSocial />
  return <ProfileResults tab={tab} />
}

function ProfileResults({ tab }: { tab: number }) {
  const rank = useBattleRank()
  const exams = useAllExams(tab !== 2)
  const battles = useProfileBattles(tab !== 1)
  const completed = exams.data?.filter(exam => exam.status === 'Completed') ?? []
  const graded = completed.filter(exam => exam.scorePercent !== null)
  const average = graded.length ? `${(graded.reduce((sum, exam) => sum + exam.scorePercent!, 0) / graded.length).toFixed(1)}%` : '—'
  const wins = battles.data?.filter(battle => battle.rank === 1).length
  const { t } = useTranslation()
  const activities = [
    ...(exams.data ?? []).map(exam => ({ id: exam.id, date: exam.completedAt ?? exam.startedAt, label: exam.status === 'Completed' ? t('profile.activity.examCompleted') : t('profile.activity.examStarted'), detail: exam.source.documentName ?? t('profile.activity.exam'), to: exam.status === 'Completed' ? `/exams/${exam.id}/result` : `/exams/${exam.id}` })),
    ...(battles.data ?? []).map(battle => ({ id: battle.battleId, date: battle.playedAt, label: t('profile.activity.battleCompleted'), detail: `${t('profile.place', { rank: battle.rank })} · ${t('profile.points', { count: battle.score })}`, to: '/exams' })),
  ].sort((a, b) => Date.parse(b.date) - Date.parse(a.date)).slice(0, 5)
  return <div className="space-y-4">
    {(tab === 0 || tab === 1) && exams.isError && <Alert title={t('profile.activity.examsFailed')}>{exams.error.message}</Alert>}
    {(tab === 0 || tab === 2) && battles.isError && <Alert title={t('profile.activity.battlesFailed')}>{battles.error.message}</Alert>}
    {((tab !== 2 && exams.isPending) || (tab !== 1 && battles.isPending)) && <p role="status" className="animate-pulse text-sm text-slate-500">{t('profile.activity.loading')}</p>}
    {(tab === 0 || tab === 2) && <RankSummary />}
    {tab === 0 && <>
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        {[[t('profile.activity.completedExams'), exams.data ? completed.length : '—'], [t('profile.activity.average'), average], [t('profile.activity.battleWins'), wins ?? '—'], [t('profile.activity.battleRating'), rank.data?.rating ?? '—']].map(([label, value]) => <div key={label} className={card}><p className="text-xs text-slate-500">{label}</p><p className="mt-2 text-xl font-semibold tabular-nums">{value}</p></div>)}
      </div>
      <section className={card}><h2 className="font-semibold">{t('profile.activity.recent')}</h2>
        {activities.length ? <ul className="mt-3 divide-y divide-slate-200">{activities.map(activity => <li key={activity.id} className="flex flex-wrap items-center justify-between gap-2 py-3 text-sm"><div className="min-w-0"><Link className="font-medium hover:text-indigo-600" to={activity.to}>{activity.label}</Link><p className="mt-1 text-slate-500">{activity.detail}</p></div><time className="text-xs text-slate-500" dateTime={activity.date}>{formatDate(activity.date)}</time></li>)}</ul>
          : !exams.isPending && !battles.isPending && !exams.isError && !battles.isError && <p className="mt-3 text-sm text-slate-500">{t('profile.activity.none')}</p>}
      </section>
    </>}
    {tab === 1 && exams.data && <>
      {!exams.data.length ? <EmptyState title={t('profile.activity.noExams')} description={t('profile.activity.noExamsText')} action={<Link to="/exams/new" className="text-indigo-600">{t('profile.activity.createExam')}</Link>} />
        : <section className={card}><div className="flex flex-wrap items-center justify-between gap-2"><h2 className="font-semibold">{t('profile.activity.recentExams')}</h2><Link to="/exams" className="text-sm text-indigo-600">{t('profile.activity.allHistory')}</Link></div><ul className="mt-3 divide-y divide-slate-200">{exams.data.slice(0, 10).map(exam => <li key={exam.id} className="py-4"><Link className="text-sm font-medium hover:text-indigo-600" to={exam.status === 'Completed' ? `/exams/${exam.id}/result` : `/exams/${exam.id}`}>{exam.source.documentName ?? t('profile.activity.docDeleted')}</Link><div className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-xs text-slate-500"><time dateTime={exam.startedAt}>{formatDate(exam.startedAt)}</time><span>{t('common.questions', { count: exam.questionCount })}</span><span>{exam.status === 'Completed' ? t('profile.activity.completed') : t('profile.activity.inProgress')}</span><span>{exam.scorePercent === null ? t('profile.activity.noResult') : `${exam.scorePercent.toFixed(1)}%`}</span></div></li>)}</ul></section>}
    </>}
    {tab === 2 && battles.data && <>
      <div className="grid grid-cols-3 gap-2">{[[t('profile.activity.played'), battles.data.length], [t('profile.activity.wins'), wins ?? 0], [t('profile.activity.otherResults'), battles.data.filter(battle => battle.rank !== 1).length]].map(([label, value]) => <div key={label} className={card}><p className="text-xs text-slate-500">{label}</p><p className="mt-2 text-xl font-semibold">{value}</p></div>)}</div>
      {!battles.data.length ? <EmptyState title={t('profile.activity.noBattles')} description={t('profile.activity.noBattlesText')} action={<Link to="/battle" className="text-indigo-600">{t('profile.activity.goBattle')}</Link>} />
        : <section className={card}><h2 className="font-semibold">{t('profile.activity.recentBattles')}</h2><ul className="mt-3 divide-y divide-slate-200">{battles.data.slice(0, 10).map(battle => <li key={battle.battleId} className="flex flex-wrap items-center justify-between gap-3 py-3 text-sm"><div><p className="font-medium">{battle.source.documentName ?? 'Battle'} · {t('profile.place', { rank: battle.rank })}</p><p className="mt-1 text-xs text-slate-500">{formatDate(battle.playedAt)} · {t('common.questions', { count: battle.questionCount })} · {t('profile.participants', { count: battle.participantCount })}</p></div><span className="font-semibold tabular-nums text-indigo-600">{t('profile.points', { count: battle.score })}{battle.ratingAfter != null && battle.ratingBefore != null && <span className="ml-2 text-xs">({battle.ratingAfter - battle.ratingBefore >= 0 ? "+" : ""}{battle.ratingAfter - battle.ratingBefore} {t('profile.ratingWord')})</span>}</span></li>)}</ul><Link to="/exams" className="mt-3 inline-block text-sm text-indigo-600">{t('profile.activity.allResults')}</Link></section>}
      <p className="text-xs text-slate-500">{t('profile.activity.ratingNote')}</p>
      <RankLeaderboard />
    </>}
  </div>
}

function ProfileSocial() {
  const friends = useFriends()
  const [inviting, setInviting] = useState<Friend | null>(null)
  const [sent, setSent] = useState(false)
  const { t } = useTranslation()
  return <div className="space-y-4">
    {sent && <div role="status"><Alert tone="info" title={t('profile.social.inviteSent')} /></div>}
    {friends.isPending && <p role="status" className="animate-pulse text-sm text-slate-500">{t('profile.social.loading')}</p>}
    {friends.isError && <Alert title={t('profile.social.failed')}>{friends.error.message}</Alert>}
    {friends.data && (!friends.data.friends.length ? <EmptyState title={t('profile.social.empty')} description={t('profile.social.emptyText')} action={<Link to="/friends" className="text-indigo-600">{t('profile.social.find')}</Link>} />
      : <section className={card}><div className="flex flex-wrap items-center justify-between gap-2"><h2 className="font-semibold">{t('profile.social.title', { n: friends.data.friends.length })}</h2><Link to="/friends" className="text-sm text-indigo-600">{t('profile.social.manage')}</Link></div><ul className="mt-3 divide-y divide-slate-200">{friends.data.friends.map(friend => <li key={friend.id} className="flex flex-wrap items-center gap-3 py-3">
        <FriendAvatar friend={friend} /><div className="min-w-0 flex-1"><p className="break-words text-sm font-medium">{friend.displayName}</p><p className="mt-1 text-xs text-slate-500">{friend.isOnline ? t('common.online') : t('common.offline')}</p></div><Button variant="secondary" className="text-xs" onClick={() => { setSent(false); setInviting(friend) }}>{t('profile.social.invite')}</Button>
      </li>)}</ul></section>)}
    {inviting && <BattleInviteModal friend={inviting} onClose={() => setInviting(null)} onSent={() => { setInviting(null); setSent(true) }} />}
  </div>
}

function FriendAvatar({ friend }: { friend: Friend }) {
  const [failed, setFailed] = useState<string | null>(null)
  return friend.avatarUrl && failed !== friend.avatarUrl ? <img src={friend.avatarUrl} alt="" onError={() => setFailed(friend.avatarUrl)} className="size-10 shrink-0 rounded-full object-cover" /> : <span aria-hidden className="grid size-10 shrink-0 place-items-center rounded-full bg-indigo-50 text-sm font-semibold text-indigo-600">{friend.displayName.slice(0, 1).toUpperCase()}</span>
}
