import { useEffect, useState } from 'react'
import { Alert } from '../../components/common/Alert'
import { Button } from '../../components/common/Button'
import { EmptyState } from '../../components/common/EmptyState'
import { useAdminUserSearch, type AdminUser } from '../admin/api'
import { useNameplateGrant, useUserNameplate } from './api'
import { Nameplate } from './Nameplate'

export function NameplateAdmin() {
  const [term, setTerm] = useState('')
  const [query, setQuery] = useState('')
  const [person, setPerson] = useState<AdminUser | null>(null)
  const search = useAdminUserSearch(query)
  useEffect(() => { const timer = setTimeout(() => setQuery(term.trim()), 250); return () => clearTimeout(timer) }, [term])
  return <div className="space-y-5">
    <div><h2 className="font-semibold">Profil effektlərinə icazə</h2><p className="mt-1 text-sm text-muted">İstifadəçi seçin və xüsusi effektləri ayrı-ayrılıqda açın və ya bağlayın. Adminlər bütün effektlərdən məhdudiyyətsiz istifadə edir.</p></div>
    <label className="block"><span className="mb-2 block text-sm font-medium">Nickname və ya e-poçt ilə axtar</span>
      <input type="search" value={term} maxLength={256} onChange={e => setTerm(e.target.value)} placeholder="Ən azı 2 simvol" className="w-full rounded-xl border border-line bg-surface px-3 py-2.5" />
    </label>
    {query.length >= 2 && <>
      {search.isFetching && <p role="status" className="animate-pulse text-muted">Axtarılır…</p>}
      {search.error && <Alert title="Axtarış alınmadı">{search.error.message}</Alert>}
      {search.data?.length === 0 && <EmptyState title="İstifadəçi tapılmadı" />}
      <div className="flex flex-wrap gap-2">{search.data?.map(user => <Button key={user.id} variant={person?.id === user.id ? 'primary' : 'secondary'} aria-pressed={person?.id === user.id} onClick={() => setPerson(user)}>{user.nickname}{user.isAdmin ? ' · Admin' : ''}</Button>)}</div>
    </>}
    {person ? <UserEffects key={person.id} person={person} /> : <EmptyState title="İstifadəçi seçin" description="İcazələr seçdiyiniz istifadəçi üçün saxlanılacaq." />}
  </div>
}

function UserEffects({ person }: { person: AdminUser }) {
  const state = useUserNameplate(person.id)
  const grant = useNameplateGrant(person.id)
  if (state.isPending) return <p role="status" className="animate-pulse text-muted">İcazələr yüklənir…</p>
  if (state.error) return <Alert title="İcazələr yüklənmədi">{state.error.message}</Alert>
  return <section className="space-y-4" aria-label={`${person.nickname} — effekt icazələri`}>
    <h3 className="font-semibold">{person.nickname}</h3>
    {state.data.isAdmin && <Alert tone="info" title="Bu istifadəçi admindir">Bütün effektlər açıqdır. Əlavə icazə tələb olunmur.</Alert>}
    {grant.error && <Alert title="İcazə dəyişdirilmədi">{grant.error.message}</Alert>}
    {grant.isSuccess && <p role="status" className="text-sm text-emerald-600">İcazə yeniləndi.</p>}
    <div className="grid min-w-0 gap-4 xl:grid-cols-2">{state.data.options.filter(x => x.restricted).map(option => <div key={option.id} className="min-w-0 rounded-xl border border-line bg-surface p-3">
      <Nameplate effect={option.id} nickname={person.nickname} avatarUrl={person.avatarUrl} />
      <div className="mt-3 flex flex-wrap items-center justify-between gap-2"><h4 className="text-sm font-semibold">{option.name}</h4>
        <Button variant="secondary" disabled={state.data.isAdmin || grant.isPending} onClick={() => grant.mutate({ id: option.id, granted: !option.granted })}>
          {state.data.isAdmin ? 'Məhdudiyyətsiz' : option.granted ? 'İcazəni geri al' : 'İcazə ver'}
        </Button>
      </div>
    </div>)}</div>
    <p className="text-xs text-muted">İcazə geri alınanda istifadəçinin həmin aktiv effekti də söndürülür. Açıq səhifələr ən gec bir dəqiqə ərzində yenilənir.</p>
  </section>
}
