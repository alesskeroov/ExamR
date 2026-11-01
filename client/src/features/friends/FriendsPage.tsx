import { Search, Users } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { Alert } from '../../components/common/Alert'
import { Button } from '../../components/common/Button'
import { EmptyState } from '../../components/common/EmptyState'
import { PageHeader } from '../../components/common/PageHeader'
import { cn } from '../../lib/utils'
import { BattleInviteModal } from './BattleInviteModal'
import { useFriendAction, useFriends, useUserSearch, type Friend, type FriendAction } from './api'

const tabs = [{ key: 'friends', label: 'Dostlar' }, { key: 'incoming', label: 'Gələn sorğular' },
  { key: 'outgoing', label: 'Göndərilən sorğular' }, { key: 'blocked', label: 'Bloklanmışlar' }] as const

export function FriendsPage() {
  const friends = useFriends()
  const change = useFriendAction()
  const [tab, setTab] = useState<typeof tabs[number]['key']>('friends')
  const [onlineOnly, setOnlineOnly] = useState(false)
  const [input, setInput] = useState('')
  const [term, setTerm] = useState('')
  const search = useUserSearch(term)
  const [inviting, setInviting] = useState<Friend | null>(null)
  const [notice, setNotice] = useState('')
  function act(id: string, action: FriendAction) {
    setNotice('')
    change.mutate({ id, action }, { onSuccess: () => setNotice(action === 'request' ? 'Dostluq sorğusu göndərildi.' : 'Dəyişiklik saxlanıldı.') })
  }
  const people = friends.data?.[tab].filter(p => tab !== 'friends' || !onlineOnly || p.isOnline) ?? []
  return (
    <>
      <PageHeader title="Dostlar" description="Dostlarınızı tapın, əlaqədə qalın və birlikdə Battle yarışına qoşulun." />
      <section className="mb-6 rounded-xl border border-slate-200 bg-white p-5">
        <h2 className="font-semibold">İstifadəçi axtar</h2>
        <form onSubmit={e => { e.preventDefault(); setTerm(input.trim()) }} className="mt-3 flex gap-2">
          <label className="sr-only" htmlFor="friend-search">Nickname</label>
          <input id="friend-search" value={input} onChange={e => setInput(e.target.value)} maxLength={100} placeholder="Nickname"
            className="h-10 min-w-0 flex-1 rounded-lg border border-slate-300 px-3" />
          <Button type="submit" variant="secondary" disabled={input.trim().length < 2}><Search className="size-4" aria-hidden />Axtar</Button>
        </form>
        <p className="mt-2 text-xs text-slate-500">Axtarış yalnız nickname üzrə aparılır.</p>
        {term.length >= 2 && <div className="mt-4 space-y-2">
          {search.isFetching && <p role="status" className="text-slate-500">Axtarılır…</p>}
          {search.isError && <Alert title="Axtarış alınmadı">{search.error.message}</Alert>}
          {search.data?.length === 0 && <p className="text-slate-500">İstifadəçi tapılmadı.</p>}
          {search.data?.map(person => <PersonRow key={person.id} person={person}>
            {person.relationship === 'None' ? <Button variant="secondary" disabled={change.isPending} onClick={() => act(person.id, 'request')}>Dost əlavə et</Button>
              : <span className="text-xs text-slate-500">{person.relationship === 'Friend' ? 'Dostunuzdur' : person.relationship === 'Incoming' ? 'Gələn sorğu var' : 'Sorğu göndərilib'}</span>}
            <Button variant="ghost" disabled={change.isPending} onClick={() => act(person.id, 'block')}>Blokla</Button>
          </PersonRow>)}
        </div>}
      </section>
      {change.isError && <div className="mb-4"><Alert title="Əməliyyat alınmadı">{change.error.message}</Alert></div>}
      {notice && <p role="status" className="mb-4 rounded-lg bg-emerald-50 px-4 py-3 text-emerald-800">{notice}</p>}
      <div className="mb-4 flex flex-wrap gap-2" role="tablist" aria-label="Dostluq bölmələri">
        {tabs.map(t => <button key={t.key} role="tab" aria-selected={tab === t.key} aria-controls="friends-panel" id={`friends-tab-${t.key}`} onClick={() => setTab(t.key)}
          className={cn('rounded-lg px-3 py-2 font-medium', tab === t.key ? 'bg-indigo-50 text-indigo-700' : 'text-slate-600 hover:bg-slate-100')}>
          {t.label} <span className="ml-1 text-xs tabular-nums">{friends.data?.[t.key].length ?? 0}</span>
        </button>)}
      </div>
      <section id="friends-panel" role="tabpanel" aria-labelledby={`friends-tab-${tab}`}>
        {tab === 'friends' && <label className="mb-4 flex items-center gap-2 text-slate-600">
          <input type="checkbox" checked={onlineOnly} onChange={e => setOnlineOnly(e.target.checked)} className="accent-indigo-600" />
          Yalnız onlayn dostlar <span className="text-xs">({friends.data?.friends.filter(p => p.isOnline).length ?? 0})</span>
        </label>}
        {friends.isPending && <p role="status" className="py-8 text-slate-500">Yüklənir…</p>}
        {friends.isError && <Alert title="Dostlar yüklənmədi">{friends.error.message}</Alert>}
        {friends.isSuccess && people.length === 0 && <EmptyState title={onlineOnly && tab === 'friends' ? 'Onlayn dost yoxdur' : 'Bu siyahı hələ boşdur'}
          description="Ad və ya istifadəçi adı ilə axtararaq dost əlavə edə bilərsiniz." icon={<Users className="size-6" />} />}
        <div className="space-y-3">{people.map(person => <PersonRow key={person.id} person={person} showOnline={tab === 'friends'}>
          {tab === 'friends' && <><Button variant="secondary" onClick={() => setInviting(person)}>Battle-a dəvət et</Button>
            <Button variant="ghost" disabled={change.isPending} onClick={() => act(person.id, 'remove')}>Dostluqdan çıxar</Button>
            <Button variant="ghost" disabled={change.isPending} onClick={() => act(person.id, 'block')}>Blokla</Button></>}
          {tab === 'incoming' && <><Button disabled={change.isPending} onClick={() => act(person.id, 'accept')}>Qəbul et</Button>
            <Button variant="secondary" disabled={change.isPending} onClick={() => act(person.id, 'reject')}>Rədd et</Button>
            <Button variant="ghost" disabled={change.isPending} onClick={() => act(person.id, 'block')}>Blokla</Button></>}
          {tab === 'outgoing' && <Button variant="secondary" disabled={change.isPending} onClick={() => act(person.id, 'remove')}>Sorğunu ləğv et</Button>}
          {tab === 'blocked' && <Button variant="secondary" disabled={change.isPending} onClick={() => act(person.id, 'unblock')}>Bloku aç</Button>}
        </PersonRow>)}</div>
      </section>
      {inviting && <BattleInviteModal friend={inviting} onClose={() => setInviting(null)} onSent={() => { setInviting(null); setNotice('Battle dəvəti göndərildi.') }} />}
    </>
  )
}

function PersonRow({ person, showOnline, children }: { person: { id: string; displayName: string; isOnline?: boolean; avatarUrl?: string | null }; showOnline?: boolean; children: ReactNode }) {
  return <div className="flex flex-col gap-3 rounded-xl border border-slate-200 bg-white p-4 sm:flex-row sm:items-center">
    <div className="flex min-w-0 flex-1 items-center gap-3">
      {person.avatarUrl ? <img src={person.avatarUrl} alt="" className="size-9 shrink-0 rounded-full object-cover" /> : <span aria-hidden className="flex size-9 shrink-0 items-center justify-center rounded-full bg-slate-100 font-medium text-slate-600">{person.displayName.charAt(0).toUpperCase()}</span>}
      <div className="min-w-0"><p className="font-medium break-words">{person.displayName}</p>
        {showOnline && <p className="mt-1 flex items-center gap-1.5 text-xs text-slate-500"><span aria-hidden className={cn('size-1.5 rounded-full', person.isOnline ? 'bg-emerald-500' : 'bg-slate-300')} />{person.isOnline ? 'Onlayn' : 'Oflayn'}</p>}
      </div>
    </div>
    <div className="flex flex-wrap items-center gap-2">{children}</div>
  </div>
}
