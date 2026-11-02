import { useEffect, useId, useRef, useState, type ReactNode } from 'react'
import { Link, Navigate, useParams } from 'react-router'
import { ArrowLeft, BookOpen, FileText, Plus, Settings2, Timer, Users, X } from 'lucide-react'
import { Alert } from '../../components/common/Alert'
import { Button } from '../../components/common/Button'
import { EmptyState } from '../../components/common/EmptyState'
import { FormField } from '../../components/common/FormField'
import { PageHeader } from '../../components/common/PageHeader'
import { formatDate } from '../../lib/format'
import { useConfirm } from '../../lib/use-confirm'
import { useCurrentUser } from '../auth/api'
import { useGroup, useGroupAction, useGroupDocuments, useGroupMembers, useGroups, useGroupUserSearch, type GroupMember, type StudyGroup } from './api'

export function GroupsPage() { return <GroupList /> }
export function GroupsAdminPage() {
  const { data: user } = useCurrentUser()
  return user?.roles.includes('Admin') ? <GroupList admin /> : <Navigate to="/groups" replace />
}

function GroupList({ admin = false }: { admin?: boolean }) {
  const groups = useGroups(admin)
  const action = useGroupAction()
  const confirm = useConfirm()
  const [editing, setEditing] = useState<StudyGroup | 'new' | null>(null)
  const [managing, setManaging] = useState<StudyGroup | null>(null)
  return <>
    <PageHeader title={admin ? 'Qrup idarəetməsi' : 'Qruplarım'} description={admin ? 'Qrupları və üzvləri idarə et.' : 'Üzv olduğun öyrənmə qrupları.'}
      actions={admin && <Button onClick={() => { action.reset(); setEditing('new') }}><Plus className="size-4" aria-hidden />Qrup yarat</Button>} />
    {groups.isPending && <p role="status" className="py-8 text-slate-500">Qruplar yüklənir…</p>}
    {groups.error && <Alert title="Qruplar yüklənmədi">{groups.error.message}</Alert>}
    {action.error && <div className="mb-4"><Alert title="Əməliyyat alınmadı">{action.error.message}</Alert></div>}
    {groups.data?.length === 0 && <EmptyState icon={<Users className="size-6" />} title="Hələ qrup yoxdur" description={admin ? 'İlk qrupu yaratmaqla başla.' : 'Admin sizi qrupa əlavə etdikdə burada görünəcək.'} />}
    <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">{groups.data?.map(group => <article key={group.id} className="flex flex-col rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
      <div className="mb-4 flex items-center justify-between"><span className="grid size-11 place-items-center rounded-xl bg-indigo-50 text-indigo-600"><Users className="size-5" aria-hidden /></span><span className="text-xs text-slate-500">{group.memberCount} üzv</span></div>
      <Link to={`/groups/${group.id}`} className="text-lg font-semibold break-words hover:text-indigo-600">{group.name}</Link>
      <p className="mt-2 flex-1 whitespace-pre-wrap break-words text-sm leading-6 text-slate-600">{group.description || 'Təsvir əlavə edilməyib.'}</p>
      <p className="mt-4 text-xs text-slate-500">Yaradılıb: {formatDate(group.createdAt)}</p>
      {admin && <div className="mt-4 flex flex-wrap gap-2 border-t border-slate-100 pt-4">
        <Button variant="secondary" className="h-9 px-3 text-xs" onClick={() => setManaging(group)}><Settings2 className="size-3.5" aria-hidden />Üzvlər</Button>
        <Button variant="ghost" className="h-9 px-3 text-xs" onClick={() => setEditing(group)}>Redaktə et</Button>
        <Button variant="ghost" className="h-9 px-3 text-xs text-rose-600" disabled={action.isPending} onClick={async () => {
          if (await confirm({ title: 'Qrup silinsin?', description: `“${group.name}” və onun üzvlükləri silinəcək. İstifadəçi hesabları qorunacaq.`, confirmLabel: 'Qrupu sil', danger: true })) action.mutate({ kind: 'delete', id: group.id })
        }}>Sil</Button>
      </div>}
    </article>)}</div>
    {editing && <GroupEditor group={editing === 'new' ? undefined : editing} onClose={() => setEditing(null)} />}
    {managing && <MemberManager group={managing} onClose={() => setManaging(null)} />}
  </>
}

export function GroupDetailPage() {
  const { id = '' } = useParams()
  const group = useGroup(id)
  const members = useGroupMembers(id)
  const documents = useGroupDocuments(id)
  return <>
    <Link to="/groups" className="mb-5 inline-flex items-center gap-2 text-sm text-indigo-600"><ArrowLeft className="size-4" aria-hidden />Qruplarım</Link>
    {group.isPending ? <p role="status">Qrup yüklənir…</p> : group.error ? <Alert title="Qrup açılmadı">{group.error.message}</Alert> : group.data && <>
      <PageHeader title={group.data.name} description={group.data.description} />
      <p className="mb-5 text-sm text-slate-500">{group.data.memberCount} üzv · {formatDate(group.data.createdAt)}</p>
      <h2 className="mb-3 font-semibold">Paylaşılan sənədlər</h2>
      {documents.isPending && <p role="status" className="text-slate-500">Sənədlər yüklənir…</p>}
      {documents.error && <Alert title="Sənədlər yüklənmədi">{documents.error.message}</Alert>}
      {documents.data?.length === 0 && <p className="mb-6 text-sm text-slate-500">Bu qrupla hələ sənəd paylaşılmayıb.</p>}
      <ul className="mb-8 grid gap-3 sm:grid-cols-2">{documents.data?.map(doc => <li key={doc.id} className="flex flex-col gap-3 rounded-xl border border-slate-200 bg-white p-4">
        <div className="flex items-start gap-3"><span className="grid size-10 shrink-0 place-items-center rounded-xl bg-indigo-50 text-indigo-600"><FileText className="size-5" aria-hidden /></span><span className="min-w-0"><span className="block break-words font-medium">{doc.fileName}</span><span className="text-xs text-slate-500">{doc.questionCount} sual</span></span></div>
        <div className="flex flex-wrap gap-2">
          <Link to={`/study?document=${doc.id}`} className="inline-flex h-9 items-center gap-1.5 rounded-xl btn-primary px-3 text-xs font-medium"><BookOpen className="size-3.5" aria-hidden />Öyrənmə köməkçisi</Link>
          <Link to={`/exams/new?document=${doc.id}`} className="inline-flex h-9 items-center gap-1.5 rounded-xl border border-slate-300 px-3 text-xs font-medium text-slate-700 hover:bg-slate-50"><Timer className="size-3.5" aria-hidden />İmtahana başla</Link>
        </div>
      </li>)}</ul>
      <h2 className="mb-3 font-semibold">Üzvlər</h2>
      {members.isPending && <p role="status" className="text-slate-500">Üzvlər yüklənir…</p>}
      {members.error && <Alert title="Üzvlər yüklənmədi">{members.error.message}</Alert>}
      {members.data?.length === 0 && <EmptyState title="Qrupda hələ üzv yoxdur" description="Üzvləri admin əlavə edəcək." />}
      <ul className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">{members.data?.map(member => <li key={member.id} className="flex items-center gap-3 rounded-xl border border-slate-200 bg-white p-4"><Avatar member={member} /><span className="min-w-0 break-words font-medium">{member.nickname}</span></li>)}</ul>
    </>}
  </>
}

function Avatar({ member }: { member: GroupMember }) {
  return member.avatarUrl ? <img src={member.avatarUrl} alt="" className="size-10 shrink-0 rounded-full object-cover" />
    : <span aria-hidden className="grid size-10 shrink-0 place-items-center rounded-full bg-indigo-50 font-semibold text-indigo-600">{member.nickname.slice(0, 1).toUpperCase()}</span>
}

function GroupModal({ title, children, onClose, busy = false }: { title: string; children: ReactNode; onClose: () => void; busy?: boolean }) {
  const ref = useRef<HTMLDialogElement>(null)
  const titleId = useId()
  useEffect(() => { const dialog = ref.current; dialog?.showModal(); return () => dialog?.close() }, [])
  return <dialog ref={ref} aria-labelledby={titleId} onCancel={event => { event.preventDefault(); if (!busy) onClose() }}
    className="m-auto max-h-[85dvh] w-[calc(100%-2rem)] max-w-xl overflow-y-auto rounded-xl border border-slate-200 bg-white p-5 shadow-xl backdrop:bg-slate-900/40 sm:p-6">
    <div className="mb-5 flex items-center justify-between gap-3"><h2 id={titleId} className="text-lg font-semibold break-words">{title}</h2><Button variant="ghost" className="size-9 shrink-0 p-0" disabled={busy} onClick={onClose} aria-label="Bağla"><X className="size-4" aria-hidden /></Button></div>
    {children}
  </dialog>
}

function GroupEditor({ group, onClose }: { group?: StudyGroup; onClose: () => void }) {
  const action = useGroupAction()
  const [name, setName] = useState(group?.name ?? '')
  const [description, setDescription] = useState(group?.description ?? '')
  const descriptionId = useId()
  return <GroupModal title={group ? 'Qrupu redaktə et' : 'Yeni qrup'} onClose={onClose} busy={action.isPending}>
    <form className="space-y-4" onSubmit={event => { event.preventDefault(); action.mutate({ kind: 'save', id: group?.id, name, description }, { onSuccess: onClose }) }}>
      <FormField label="Qrupun adı" value={name} onChange={e => setName(e.target.value)} required maxLength={120} autoFocus disabled={action.isPending} />
      <div><label htmlFor={descriptionId} className="mb-1.5 block font-medium text-slate-700">Qısa təsvir</label><textarea id={descriptionId} value={description} onChange={e => setDescription(e.target.value)} rows={3} maxLength={500} disabled={action.isPending} className="w-full rounded-xl border border-slate-300 bg-white p-3 focus:border-indigo-500 focus:outline-none focus:ring-2 focus:ring-indigo-500/20" /></div>
      {action.error && <Alert title="Qrup saxlanmadı">{action.error.message}</Alert>}
      <div className="flex justify-end gap-2"><Button variant="secondary" disabled={action.isPending} onClick={onClose}>Ləğv et</Button><Button type="submit" loading={action.isPending} disabled={!name.trim()}>Saxla</Button></div>
    </form>
  </GroupModal>
}

function MemberManager({ group, onClose }: { group: StudyGroup; onClose: () => void }) {
  const members = useGroupMembers(group.id)
  const [term, setTerm] = useState('')
  const [query, setQuery] = useState('')
  const [selected, setSelected] = useState<string[]>([])
  const search = useGroupUserSearch(group.id, query)
  const action = useGroupAction()
  const confirm = useConfirm()
  const searchId = useId()
  useEffect(() => { const timer = setTimeout(() => setQuery(term.trim()), 250); return () => clearTimeout(timer) }, [term])
  return <GroupModal title={`${group.name} · Üzvlər`} onClose={onClose} busy={action.isPending}>
    <label htmlFor={searchId} className="mb-2 block text-sm font-medium">Nickname ilə istifadəçi axtar</label>
    <input id={searchId} type="search" autoFocus value={term} onChange={e => setTerm(e.target.value)} maxLength={24} placeholder="Ən azı 2 simvol yazın" className="h-11 w-full rounded-xl border border-slate-300 bg-white px-3 focus:border-indigo-500 focus:outline-none focus:ring-2 focus:ring-indigo-500/20" />
    {query.length >= 2 && <div className="mt-3 space-y-2">
      {(search.isPending || search.isFetching) && <p role="status" className="text-sm text-slate-500">Axtarılır…</p>}
      {search.error && <Alert title="Axtarış alınmadı">{search.error.message}</Alert>}
      {search.data?.length === 0 && <p className="text-sm text-slate-500">İstifadəçi tapılmadı.</p>}
      <ul className="max-h-56 space-y-2 overflow-y-auto">{search.data?.map(person => <li key={person.id}>
        <label className="flex cursor-pointer items-center gap-3 rounded-xl border border-slate-200 p-3"><input type="checkbox" aria-label={`${person.nickname} seç`} checked={person.isMember || selected.includes(person.id)} disabled={person.isMember || action.isPending || (!selected.includes(person.id) && selected.length >= 100)}
          onChange={e => setSelected(ids => e.target.checked ? [...ids, person.id] : ids.filter(id => id !== person.id))} className="size-4 accent-indigo-600" />
          <Avatar member={person} /><span className="min-w-0 flex-1 break-words text-sm font-medium">{person.nickname}</span>{person.isMember && <span className="text-xs text-slate-500">Üzvdür</span>}
        </label>
      </li>)}</ul>
    </div>}
    <div className="mt-4 flex items-center justify-between gap-3"><p className="text-xs text-slate-500" role="status">{selected.length} istifadəçi seçilib</p><Button disabled={!selected.length || action.isPending} onClick={() => action.mutate({ kind: 'add', id: group.id, userIds: selected }, { onSuccess: () => setSelected([]) })}>Seçilənləri əlavə et</Button></div>
    {action.error && <div className="mt-4"><Alert title="Əməliyyat alınmadı">{action.error.message}</Alert></div>}
    <h3 className="mt-6 border-t border-slate-200 pt-4 font-semibold">Qrup üzvləri · {members.data?.length ?? 0}</h3>
    {members.isPending && <p role="status" className="mt-3 text-slate-500">Yüklənir…</p>}
    {members.error && <Alert title="Üzvlər yüklənmədi">{members.error.message}</Alert>}
    {members.data?.length === 0 && <p className="mt-3 text-sm text-slate-500">Hələ üzv yoxdur.</p>}
    <ul className="mt-3 max-h-72 space-y-2 overflow-y-auto">{members.data?.map(member => <li key={member.id} className="flex items-center gap-3 rounded-xl bg-slate-50 p-3"><Avatar member={member} /><span className="min-w-0 flex-1 break-words text-sm font-medium">{member.nickname}</span>
      <Button variant="ghost" className="h-8 px-2 text-xs text-rose-600" disabled={action.isPending} onClick={async () => {
        if (await confirm({ title: 'Üzv çıxarılsın?', description: `“${member.nickname}” qrupdan çıxarılacaq.`, confirmLabel: 'Çıxar', danger: true })) action.mutate({ kind: 'remove', id: group.id, userId: member.id })
      }}>Çıxar</Button>
    </li>)}</ul>
  </GroupModal>
}
