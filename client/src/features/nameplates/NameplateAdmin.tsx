import { useEffect, useState } from 'react'
import { Alert } from '../../components/common/Alert'
import { Button } from '../../components/common/Button'
import { EmptyState } from '../../components/common/EmptyState'
import { useAdminUserSearch, type AdminUser } from '../admin/api'
import { useNameplateGrant, useUserNameplate } from './api'
import { Nameplate } from './Nameplate'
import { useTranslation } from 'react-i18next'

export function NameplateAdmin() {
  const { t } = useTranslation()
  const [term, setTerm] = useState('')
  const [query, setQuery] = useState('')
  const [person, setPerson] = useState<AdminUser | null>(null)
  const search = useAdminUserSearch(query)
  useEffect(() => { const timer = setTimeout(() => setQuery(term.trim()), 250); return () => clearTimeout(timer) }, [term])
  return <div className="space-y-5">
    <div><h2 className="font-semibold">{t('nameplates.admin.title')}</h2><p className="mt-1 text-sm text-muted">{t('nameplates.admin.text')}</p></div>
    <label className="block"><span className="mb-2 block text-sm font-medium">{t('nameplates.admin.search')}</span>
      <input type="search" value={term} maxLength={256} onChange={e => setTerm(e.target.value)} placeholder={t('admin.minChars')} className="w-full rounded-xl border border-line bg-surface px-3 py-2.5" />
    </label>
    {query.length >= 2 && <>
      {search.isFetching && <p role="status" className="animate-pulse text-muted">{t('friends.searching')}</p>}
      {search.error && <Alert title={t('friends.searchFailed')}>{search.error.message}</Alert>}
      {search.data?.length === 0 && <EmptyState title={t('friends.noUser')} />}
      <div className="flex flex-wrap gap-2">{search.data?.map(user => <Button key={user.id} variant={person?.id === user.id ? 'primary' : 'secondary'} aria-pressed={person?.id === user.id} onClick={() => setPerson(user)}>{user.nickname}{user.isAdmin ? ' · Admin' : ''}</Button>)}</div>
    </>}
    {person ? <UserEffects key={person.id} person={person} /> : <EmptyState title={t('nameplates.admin.pickUser')} description={t('nameplates.admin.pickUserText')} />}
  </div>
}

function UserEffects({ person }: { person: AdminUser }) {
  const { t } = useTranslation()
  const state = useUserNameplate(person.id)
  const grant = useNameplateGrant(person.id)
  if (state.isPending) return <p role="status" className="animate-pulse text-muted">{t('nameplates.admin.loading')}</p>
  if (state.error) return <Alert title={t('nameplates.admin.loadFailed')}>{state.error.message}</Alert>
  return <section className="space-y-4" aria-label={t('nameplates.admin.aria', { name: person.nickname })}>
    <h3 className="font-semibold">{person.nickname}</h3>
    {state.data.isAdmin && <Alert tone="info" title={t('nameplates.admin.isAdmin')}>{t('nameplates.admin.isAdminText')}</Alert>}
    {grant.error && <Alert title={t('nameplates.admin.grantFailed')}>{grant.error.message}</Alert>}
    {grant.isSuccess && <p role="status" className="text-sm text-emerald-600">{t('nameplates.admin.updated')}</p>}
    <div className="grid min-w-0 gap-4 xl:grid-cols-2">{state.data.options.filter(x => x.restricted).map(option => <div key={option.id} className="min-w-0 rounded-xl border border-line bg-surface p-3">
      <Nameplate effect={option.id} nickname={person.nickname} avatarUrl={person.avatarUrl} />
      <div className="mt-3 flex flex-wrap items-center justify-between gap-2"><h4 className="text-sm font-semibold">{option.name}</h4>
        <Button variant="secondary" disabled={state.data.isAdmin || grant.isPending} onClick={() => grant.mutate({ id: option.id, granted: !option.granted })}>
          {state.data.isAdmin ? t('nameplates.admin.unrestricted') : option.granted ? t('nameplates.admin.revoke') : t('nameplates.admin.grant')}
        </Button>
      </div>
    </div>)}</div>
    <p className="text-xs text-muted">{t('nameplates.admin.note')}</p>
  </section>
}
