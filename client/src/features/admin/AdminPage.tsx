import { NameplateAdmin } from '../nameplates/NameplateAdmin'
import { UsageAdmin } from '../usage/UsageAdmin'
import { useEffect, useId, useState } from 'react'
import { FolderOpen, School, ShieldCheck, Users } from 'lucide-react'
import { DimAdminSection } from '../dim/DimAdminSection'
import { Navigate, useSearchParams } from 'react-router'
import { Alert } from '../../components/common/Alert'
import { Button } from '../../components/common/Button'
import { PageHeader } from '../../components/common/PageHeader'
import { useConfirm } from '../../lib/use-confirm'
import { useCurrentUser } from '../auth/api'
import { SystemStatus } from '../dashboard/DashboardPage'
import { UploadPage } from '../documents/UploadPage'
import { GroupsAdminPage } from '../groups/GroupsPage'
import { cn } from '../../lib/utils'
import { useAdminRole, useAdmins, useAdminUserSearch, type AdminUser } from './api'
import { useTranslation } from 'react-i18next'

const sections = [
  { id: 'nameplates', icon: Users },
  { id: 'usage', icon: Users },
  { id: 'documents', icon: FolderOpen },
  { id: 'dim', icon: School },
  { id: 'groups', icon: Users },
  { id: 'admins', icon: ShieldCheck },
]

export function AdminPage() {
  const { t } = useTranslation()
  const { data: user } = useCurrentUser()
  const [params, setParams] = useSearchParams()
  if (!user?.roles.includes('Admin')) return <Navigate to="/" replace />
  const section = sections.some(item => item.id === params.get('section')) ? params.get('section') : 'documents'
  return <>
    <PageHeader title={t('nav.items.admin')} description={t('admin.description')} />
    <SystemStatus />
    <nav aria-label={t('admin.sectionsAria')} className="mb-6 flex flex-wrap gap-2 rounded-xl border border-slate-200 bg-white p-2">
      {sections.map(item => {
        const active = section === item.id
        return <button key={item.id} type="button" aria-pressed={active} onClick={() => setParams({ section: item.id })} className={cn('flex items-center gap-2 rounded-lg px-4 py-2.5 text-sm font-medium transition-colors', active ? 'bg-indigo-50 text-indigo-700' : 'text-slate-600 hover:bg-slate-50')}><item.icon className="size-4" aria-hidden />{t(`admin.sections.${item.id}`)}</button>
      })}
    </nav>
    {section === 'nameplates' ? <NameplateAdmin /> : section === 'usage' ? <UsageAdmin /> : section === 'dim' ? <DimAdminSection /> : section === 'groups' ? <GroupsAdminPage /> : section === 'admins' ? <AdminsSection currentUserId={user.id} /> : <UploadPage admin />}
  </>
}

function AdminsSection({ currentUserId }: { currentUserId: string }) {
  const { t } = useTranslation()
  const admins = useAdmins()
  const role = useAdminRole()
  const confirm = useConfirm()
  const [term, setTerm] = useState('')
  const [query, setQuery] = useState('')
  const [notice, setNotice] = useState('')
  const search = useAdminUserSearch(query)
  const searchId = useId()
  useEffect(() => { const timer = setTimeout(() => setQuery(term.trim()), 250); return () => clearTimeout(timer) }, [term])

  async function change(person: AdminUser, grant: boolean) {
    const ok = await confirm(grant
      ? { title: t('admin.grantTitle'), description: t('admin.grantText', { name: person.nickname }), confirmLabel: t('admin.grant') }
      : { title: t('admin.revokeTitle'), description: t('admin.revokeText', { name: person.nickname }), confirmLabel: t('admin.revoke'), danger: true })
    if (!ok) return
    setNotice('')
    role.mutate({ userId: person.id, grant }, { onSuccess: () => setNotice(grant ? t('admin.granted', { name: person.nickname }) : t('admin.revoked', { name: person.nickname })) })
  }

  const row = (person: AdminUser) => <li key={person.id} className="flex flex-wrap items-center gap-3 rounded-xl border border-slate-200 bg-white p-3">
    {person.avatarUrl ? <img src={person.avatarUrl} alt="" className="size-10 shrink-0 rounded-full object-cover" />
      : <span aria-hidden className="grid size-10 shrink-0 place-items-center rounded-full bg-indigo-50 font-semibold text-indigo-600">{person.nickname.slice(0, 1).toUpperCase()}</span>}
    <span className="min-w-0 flex-1"><span className="block break-words text-sm font-medium">{person.nickname}</span><span className="block break-all text-xs text-slate-500">{person.email}</span></span>
    {person.isAdmin
      ? person.id === currentUserId ? <span className="text-xs text-slate-500">{t('battle.chat.you')}</span>
        : <Button variant="ghost" className="h-9 px-3 text-xs text-rose-600" disabled={role.isPending} onClick={() => change(person, false)}>{t('admin.revoke')}</Button>
      : <Button variant="secondary" className="h-9 px-3 text-xs" disabled={role.isPending} onClick={() => change(person, true)}>{t('admin.makeAdmin')}</Button>}
  </li>

  return <div className="space-y-6">
    {notice && <Alert tone="info" title={t('admin.success')}>{notice}</Alert>}
    {role.error && <Alert title={t('profile.actionFailed')}>{role.error.message}</Alert>}
    <section>
      <label htmlFor={searchId} className="mb-2 block text-sm font-medium">{t('admin.searchLabel')}</label>
      <input id={searchId} type="search" value={term} onChange={e => setTerm(e.target.value)} maxLength={256} placeholder={t('admin.minChars')} className="h-11 w-full rounded-xl border border-slate-300 bg-white px-3 focus:border-indigo-500 focus:outline-none focus:ring-2 focus:ring-indigo-500/20" />
      {query.length >= 2 && <div className="mt-3 space-y-2">
        {search.isFetching && <p role="status" className="text-sm text-slate-500">{t('friends.searching')}</p>}
        {search.error && <Alert title={t('friends.searchFailed')}>{search.error.message}</Alert>}
        {search.data?.length === 0 && <p className="text-sm text-slate-500">{t('friends.noUser')}</p>}
        <ul className="space-y-2">{search.data?.map(row)}</ul>
      </div>}
    </section>
    <section>
      <h2 className="mb-3 font-semibold">{t('admin.current', { n: admins.data?.length ?? 0 })}</h2>
      {admins.isPending && <p role="status" className="text-slate-500">{t('common.loadingDots')}</p>}
      {admins.error && <Alert title={t('admin.loadFailed')}>{admins.error.message}</Alert>}
      <ul className="grid gap-2 lg:grid-cols-2">{admins.data?.map(row)}</ul>
    </section>
  </div>
}
