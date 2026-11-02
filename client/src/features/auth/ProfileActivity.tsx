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

const card = 'rounded-xl border border-slate-200 bg-white p-5'

export function ProfileActivity({ tab }: { tab: number }) {
  // Mount only the active section: the social polling hook stays off other tabs.
  if (tab === 3) return <ProfileSocial />
  return <ProfileResults tab={tab} />
}

function ProfileResults({ tab }: { tab: number }) {
  const exams = useAllExams(tab !== 2)
  const battles = useProfileBattles(tab !== 1)
  const completed = exams.data?.filter(exam => exam.status === 'Completed') ?? []
  const graded = completed.filter(exam => exam.scorePercent !== null)
  const average = graded.length ? `${(graded.reduce((sum, exam) => sum + exam.scorePercent!, 0) / graded.length).toFixed(1)}%` : '—'
  const wins = battles.data?.filter(battle => battle.rank === 1).length
  const activities = [
    ...(exams.data ?? []).map(exam => ({ id: exam.id, date: exam.completedAt ?? exam.startedAt, label: exam.status === 'Completed' ? 'İmtahan tamamlandı' : 'İmtahan başladı', detail: exam.source.documentName ?? 'İmtahan', to: exam.status === 'Completed' ? `/exams/${exam.id}/result` : `/exams/${exam.id}` })),
    ...(battles.data ?? []).map(battle => ({ id: battle.battleId, date: battle.playedAt, label: 'Battle tamamlandı', detail: `${battle.rank}-ci yer · ${battle.score} xal`, to: '/exams' })),
  ].sort((a, b) => Date.parse(b.date) - Date.parse(a.date)).slice(0, 5)
  return <div className="space-y-4">
    {(tab === 0 || tab === 1) && exams.isError && <Alert title="İmtahanlar yüklənmədi">{exams.error.message}</Alert>}
    {(tab === 0 || tab === 2) && battles.isError && <Alert title="Battle nəticələri yüklənmədi">{battles.error.message}</Alert>}
    {((tab !== 2 && exams.isPending) || (tab !== 1 && battles.isPending)) && <p role="status" className="animate-pulse text-sm text-slate-500">Nəticələr yüklənir…</p>}
    {tab === 0 && <>
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        {[['Tamamlanan imtahan', exams.data ? completed.length : '—'], ['Orta nəticə', average], ['Battle qələbəsi', wins ?? '—'], ['Battle reytinqi', 'Mövcud deyil']].map(([label, value]) => <div key={label} className={card}><p className="text-xs text-slate-500">{label}</p><p className="mt-2 text-xl font-semibold tabular-nums">{value}</p></div>)}
      </div>
      <section className={card}><h2 className="font-semibold">Son aktivlik</h2>
        {activities.length ? <ul className="mt-3 divide-y divide-slate-200">{activities.map(activity => <li key={activity.id} className="flex flex-wrap items-center justify-between gap-2 py-3 text-sm"><div className="min-w-0"><Link className="font-medium hover:text-indigo-600" to={activity.to}>{activity.label}</Link><p className="mt-1 text-slate-500">{activity.detail}</p></div><time className="text-xs text-slate-500" dateTime={activity.date}>{formatDate(activity.date)}</time></li>)}</ul>
          : !exams.isPending && !battles.isPending && !exams.isError && !battles.isError && <p className="mt-3 text-sm text-slate-500">Hələ aktivlik yoxdur. İlk imtahanınızı və ya Battle oyununu başladın.</p>}
      </section>
    </>}
    {tab === 1 && exams.data && <>
      {!exams.data.length ? <EmptyState title="Hələ imtahanınız yoxdur" description="İlk imtahanınızdan sonra nəticələr burada görünəcək." action={<Link to="/exams/new" className="text-indigo-600">İmtahan yarat</Link>} />
        : <section className={card}><div className="flex flex-wrap items-center justify-between gap-2"><h2 className="font-semibold">Son imtahanlar</h2><Link to="/exams" className="text-sm text-indigo-600">Bütün tarixçə</Link></div><ul className="mt-3 divide-y divide-slate-200">{exams.data.slice(0, 10).map(exam => <li key={exam.id} className="py-4"><Link className="text-sm font-medium hover:text-indigo-600" to={exam.status === 'Completed' ? `/exams/${exam.id}/result` : `/exams/${exam.id}`}>{exam.source.documentName ?? 'Sənəd silinib'}</Link><div className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-xs text-slate-500"><time dateTime={exam.startedAt}>{formatDate(exam.startedAt)}</time><span>{exam.questionCount} sual</span><span>{exam.status === 'Completed' ? 'Tamamlanıb' : 'Davam edir'}</span><span>{exam.scorePercent === null ? 'Nəticə yoxdur' : `${exam.scorePercent.toFixed(1)}%`}</span></div></li>)}</ul></section>}
    </>}
    {tab === 2 && battles.data && <>
      <div className="grid grid-cols-3 gap-2">{[['Oynanılan Battle', battles.data.length], ['Qələbə', wins ?? 0], ['Digər nəticələr', battles.data.filter(battle => battle.rank !== 1).length]].map(([label, value]) => <div key={label} className={card}><p className="text-xs text-slate-500">{label}</p><p className="mt-2 text-xl font-semibold">{value}</p></div>)}</div>
      {!battles.data.length ? <EmptyState title="Hələ Battle nəticəniz yoxdur" description="İlk yarışınızdan sonra nəticələr burada görünəcək." action={<Link to="/battle" className="text-indigo-600">Battle-a keç</Link>} />
        : <section className={card}><h2 className="font-semibold">Son Battle nəticələri</h2><ul className="mt-3 divide-y divide-slate-200">{battles.data.slice(0, 10).map(battle => <li key={battle.battleId} className="flex flex-wrap items-center justify-between gap-3 py-3 text-sm"><div><p className="font-medium">{battle.source.documentName ?? 'Battle'} · {battle.rank}-ci yer</p><p className="mt-1 text-xs text-slate-500">{formatDate(battle.playedAt)} · {battle.questionCount} sual · {battle.participantCount} iştirakçı</p></div><span className="font-semibold tabular-nums text-indigo-600">{battle.score} xal</span></li>)}</ul><Link to="/exams" className="mt-3 inline-block text-sm text-indigo-600">Bütün nəticələr</Link></section>}
      <p className="text-xs text-slate-500">Ümumi reytinq sistemi hələ mövcud deyil. Yer hər yarışın öz nəticəsidir.</p>
    </>}
  </div>
}

function ProfileSocial() {
  const friends = useFriends()
  const [inviting, setInviting] = useState<Friend | null>(null)
  const [sent, setSent] = useState(false)
  return <div className="space-y-4">
    {sent && <div role="status"><Alert tone="info" title="Battle dəvəti göndərildi." /></div>}
    {friends.isPending && <p role="status" className="animate-pulse text-sm text-slate-500">Dostlar yüklənir…</p>}
    {friends.isError && <Alert title="Dostlar yüklənmədi">{friends.error.message}</Alert>}
    {friends.data && (!friends.data.friends.length ? <EmptyState title="Dost siyahınız boşdur" description="Dostlar əlavə edin və birlikdə Battle oynayın." action={<Link to="/friends" className="text-indigo-600">Dostları tap</Link>} />
      : <section className={card}><div className="flex flex-wrap items-center justify-between gap-2"><h2 className="font-semibold">Dostlar · {friends.data.friends.length}</h2><Link to="/friends" className="text-sm text-indigo-600">Dostları idarə et</Link></div><ul className="mt-3 divide-y divide-slate-200">{friends.data.friends.map(friend => <li key={friend.id} className="flex flex-wrap items-center gap-3 py-3">
        <FriendAvatar friend={friend} /><div className="min-w-0 flex-1"><p className="break-words text-sm font-medium">{friend.displayName}</p><p className="mt-1 text-xs text-slate-500">{friend.isOnline ? 'Onlayn' : 'Oflayn'}</p></div><Button variant="secondary" className="text-xs" onClick={() => { setSent(false); setInviting(friend) }}>Battle-a dəvət et</Button>
      </li>)}</ul></section>)}
    {inviting && <BattleInviteModal friend={inviting} onClose={() => setInviting(null)} onSent={() => { setInviting(null); setSent(true) }} />}
  </div>
}

function FriendAvatar({ friend }: { friend: Friend }) {
  const [failed, setFailed] = useState<string | null>(null)
  return friend.avatarUrl && failed !== friend.avatarUrl ? <img src={friend.avatarUrl} alt="" onError={() => setFailed(friend.avatarUrl)} className="size-10 rounded-full object-cover" /> : <span aria-hidden className="grid size-10 shrink-0 place-items-center rounded-full bg-indigo-50 text-sm font-semibold text-indigo-600">{friend.displayName.slice(0, 1).toUpperCase()}</span>
}
