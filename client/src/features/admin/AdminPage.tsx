import { useEffect, useId, useState } from 'react'
import { FolderOpen, ShieldCheck, Users } from 'lucide-react'
import { Navigate, useSearchParams } from 'react-router'
import { Alert } from '../../components/common/Alert'
import { Button } from '../../components/common/Button'
import { PageHeader } from '../../components/common/PageHeader'
import { useConfirm } from '../../lib/use-confirm'
import { useCurrentUser } from '../auth/api'
import { UploadPage } from '../documents/UploadPage'
import { GroupsAdminPage } from '../groups/GroupsPage'
import { cn } from '../../lib/utils'
import { useAdminRole, useAdmins, useAdminUserSearch, type AdminUser } from './api'

const sections = [
  { id: 'documents', label: 'Sənədlər', icon: FolderOpen },
  { id: 'groups', label: 'Qruplar', icon: Users },
  { id: 'admins', label: 'Adminlər', icon: ShieldCheck },
]

export function AdminPage() {
  const { data: user } = useCurrentUser()
  const [params, setParams] = useSearchParams()
  if (!user?.roles.includes('Admin')) return <Navigate to="/" replace />
  const section = sections.some(item => item.id === params.get('section')) ? params.get('section') : 'documents'
  return <>
    <PageHeader title="Admin Panel" description="Sənədləri, qrupları və adminləri idarə et." />
    <nav aria-label="Admin bölmələri" className="mb-6 flex flex-wrap gap-2 rounded-xl border border-slate-200 bg-white p-2">
      {sections.map(item => {
        const active = section === item.id
        return <button key={item.id} type="button" aria-pressed={active} onClick={() => setParams({ section: item.id })} className={cn('flex items-center gap-2 rounded-lg px-4 py-2.5 text-sm font-medium transition-colors', active ? 'bg-indigo-50 text-indigo-700' : 'text-slate-600 hover:bg-slate-50')}><item.icon className="size-4" aria-hidden />{item.label}</button>
      })}
    </nav>
    {section === 'groups' ? <GroupsAdminPage /> : section === 'admins' ? <AdminsSection currentUserId={user.id} /> : <UploadPage admin />}
  </>
}

function AdminsSection({ currentUserId }: { currentUserId: string }) {
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
      ? { title: 'Admin rolu verilsin?', description: `“${person.nickname}” bütün admin bölmələrinə giriş əldə edəcək.`, confirmLabel: 'Rol ver' }
      : { title: 'Admin rolu geri alınsın?', description: `“${person.nickname}” adi istifadəçi olacaq. Hesabı, sənədləri və qrup üzvlükləri qorunur.`, confirmLabel: 'Rolu geri al', danger: true })
    if (!ok) return
    setNotice('')
    role.mutate({ userId: person.id, grant }, { onSuccess: () => setNotice(grant ? `${person.nickname} artıq admindir.` : `${person.nickname} adi istifadəçiyə çevrildi.`) })
  }

  const row = (person: AdminUser) => <li key={person.id} className="flex flex-wrap items-center gap-3 rounded-xl border border-slate-200 bg-white p-3">
    {person.avatarUrl ? <img src={person.avatarUrl} alt="" className="size-10 shrink-0 rounded-full object-cover" />
      : <span aria-hidden className="grid size-10 shrink-0 place-items-center rounded-full bg-indigo-50 font-semibold text-indigo-600">{person.nickname.slice(0, 1).toUpperCase()}</span>}
    <span className="min-w-0 flex-1"><span className="block break-words text-sm font-medium">{person.nickname}</span><span className="block break-all text-xs text-slate-500">{person.email}</span></span>
    {person.isAdmin
      ? person.id === currentUserId ? <span className="text-xs text-slate-500">Siz</span>
        : <Button variant="ghost" className="h-9 px-3 text-xs text-rose-600" disabled={role.isPending} onClick={() => change(person, false)}>Rolu geri al</Button>
      : <Button variant="secondary" className="h-9 px-3 text-xs" disabled={role.isPending} onClick={() => change(person, true)}>Admin et</Button>}
  </li>

  return <div className="space-y-6">
    {notice && <Alert tone="info" title="Uğurlu əməliyyat">{notice}</Alert>}
    {role.error && <Alert title="Əməliyyat alınmadı">{role.error.message}</Alert>}
    <section>
      <label htmlFor={searchId} className="mb-2 block text-sm font-medium">İstifadəçi axtar · nickname və ya email</label>
      <input id={searchId} type="search" value={term} onChange={e => setTerm(e.target.value)} maxLength={256} placeholder="Ən azı 2 simvol" className="h-11 w-full rounded-xl border border-slate-300 bg-white px-3 focus:border-indigo-500 focus:outline-none focus:ring-2 focus:ring-indigo-500/20" />
      {query.length >= 2 && <div className="mt-3 space-y-2">
        {search.isFetching && <p role="status" className="text-sm text-slate-500">Axtarılır…</p>}
        {search.error && <Alert title="Axtarış alınmadı">{search.error.message}</Alert>}
        {search.data?.length === 0 && <p className="text-sm text-slate-500">İstifadəçi tapılmadı.</p>}
        <ul className="space-y-2">{search.data?.map(row)}</ul>
      </div>}
    </section>
    <section>
      <h2 className="mb-3 font-semibold">Mövcud adminlər · {admins.data?.length ?? 0}</h2>
      {admins.isPending && <p role="status" className="text-slate-500">Yüklənir…</p>}
      {admins.error && <Alert title="Adminlər yüklənmədi">{admins.error.message}</Alert>}
      <ul className="grid gap-2 lg:grid-cols-2">{admins.data?.map(row)}</ul>
    </section>
  </div>
}
