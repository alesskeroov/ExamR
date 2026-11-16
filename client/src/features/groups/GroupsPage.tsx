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
import { useTranslation } from 'react-i18next'

export function GroupsPage() { return <GroupList /> }
export function GroupsAdminPage() {
  const { data: user } = useCurrentUser()
  return user?.roles.includes('Admin') ? <GroupList admin /> : <Navigate to="/groups" replace />
}

function GroupList({ admin = false }: { admin?: boolean }) {
  const { t } = useTranslation()
  const groups = useGroups(admin)
  const action = useGroupAction()
  const confirm = useConfirm()
  const [editing, setEditing] = useState<StudyGroup | 'new' | null>(null)
  const [managing, setManaging] = useState<StudyGroup | null>(null)
  return <>
    <PageHeader title={admin ? t('groups.adminTitle') : t('nav.items.groups')} description={admin ? t('groups.adminDescription') : t('groups.description')}
      actions={admin && <Button onClick={() => { action.reset(); setEditing('new') }}><Plus className="size-4" aria-hidden />{t('groups.create')}</Button>} />
    {groups.isPending && <p role="status" className="py-8 text-slate-500">{t('documents.groupsLoading')}</p>}
    {groups.error && <Alert title={t('documents.groupsFailed')}>{groups.error.message}</Alert>}
    {action.error && <div className="mb-4"><Alert title={t('profile.actionFailed')}>{action.error.message}</Alert></div>}
    {groups.data?.length === 0 && <EmptyState icon={<Users className="size-6" />} title={t('groups.empty')} description={admin ? t('groups.emptyAdmin') : t('groups.emptyText')} />}
    <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">{groups.data?.map(group => <article key={group.id} className="flex flex-col rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
      <div className="mb-4 flex items-center justify-between"><span className="grid size-11 place-items-center rounded-xl bg-indigo-50 text-indigo-600"><Users className="size-5" aria-hidden /></span><span className="text-xs text-slate-500">{t('groups.members', { count: group.memberCount })}</span></div>
      <Link to={`/groups/${group.id}`} className="text-lg font-semibold break-words hover:text-indigo-600">{group.name}</Link>
      <p className="mt-2 flex-1 whitespace-pre-wrap break-words text-sm leading-6 text-slate-600">{group.description || t('groups.noDescription')}</p>
      <p className="mt-4 text-xs text-slate-500">{t('groups.created', { date: formatDate(group.createdAt) })}</p>
      {admin && <div className="mt-4 flex flex-wrap gap-2 border-t border-slate-100 pt-4">
        <Button variant="secondary" className="h-9 px-3 text-xs" onClick={() => setManaging(group)}><Settings2 className="size-3.5" aria-hidden />{t('groups.membersTitle')}</Button>
        <Button variant="ghost" className="h-9 px-3 text-xs" onClick={() => setEditing(group)}>{t('questions.edit')}</Button>
        <Button variant="ghost" className="h-9 px-3 text-xs text-rose-600" disabled={action.isPending} onClick={async () => {
          if (await confirm({ title: t('groups.deleteTitle'), description: t('groups.deleteText', { name: group.name }), confirmLabel: t('groups.delete'), danger: true })) action.mutate({ kind: 'delete', id: group.id })
        }}>{t('common.delete')}</Button>
      </div>}
    </article>)}</div>
    {editing && <GroupEditor group={editing === 'new' ? undefined : editing} onClose={() => setEditing(null)} />}
    {managing && <MemberManager group={managing} onClose={() => setManaging(null)} />}
  </>
}

export function GroupDetailPage() {
  const { t } = useTranslation()
  const { id = '' } = useParams()
  const group = useGroup(id)
  const members = useGroupMembers(id)
  const documents = useGroupDocuments(id)
  return <>
    <Link to="/groups" className="mb-5 inline-flex items-center gap-2 text-sm text-indigo-600"><ArrowLeft className="size-4" aria-hidden />{t('nav.items.groups')}</Link>
    {group.isPending ? <p role="status">{t('groups.loading')}</p> : group.error ? <Alert title={t('groups.openFailed')}>{group.error.message}</Alert> : group.data && <>
      <PageHeader title={group.data.name} description={group.data.description} />
      <p className="mb-5 text-sm text-slate-500">{t('groups.members', { count: group.data.memberCount })} · {formatDate(group.data.createdAt)}</p>
      <h2 className="mb-3 font-semibold">{t('groups.sharedDocs')}</h2>
      {documents.isPending && <p role="status" className="text-slate-500">{t('groups.docsLoading')}</p>}
      {documents.error && <Alert title={t('documents.listFailed')}>{documents.error.message}</Alert>}
      {documents.data?.length === 0 && <p className="mb-6 text-sm text-slate-500">{t('groups.noDocs')}</p>}
      <ul className="mb-8 grid gap-3 sm:grid-cols-2">{documents.data?.map(doc => <li key={doc.id} className="flex flex-col gap-3 rounded-xl border border-slate-200 bg-white p-4">
        <div className="flex items-start gap-3"><span className="grid size-10 shrink-0 place-items-center rounded-xl bg-indigo-50 text-indigo-600"><FileText className="size-5" aria-hidden /></span><span className="min-w-0"><span className="block break-words font-medium">{doc.fileName}</span><span className="text-xs text-slate-500">{t('common.questions', { count: doc.questionCount })}</span></span></div>
        <div className="flex flex-wrap gap-2">
          <Link to={`/study?document=${doc.id}`} className="inline-flex h-9 items-center gap-1.5 rounded-xl btn-primary px-3 text-xs font-medium"><BookOpen className="size-3.5" aria-hidden />{t('groups.studyAssistant')}</Link>
          <Link to={`/exams/new?document=${doc.id}`} className="inline-flex h-9 items-center gap-1.5 rounded-xl border border-slate-300 px-3 text-xs font-medium text-slate-700 hover:bg-slate-50"><Timer className="size-3.5" aria-hidden />{t('nav.items.newExam')}</Link>
        </div>
      </li>)}</ul>
      <h2 className="mb-3 font-semibold">{t('groups.membersTitle')}</h2>
      {members.isPending && <p role="status" className="text-slate-500">{t('groups.membersLoading')}</p>}
      {members.error && <Alert title={t('groups.membersFailed')}>{members.error.message}</Alert>}
      {members.data?.length === 0 && <EmptyState title={t('groups.noMembers')} description={t('groups.noMembersText')} />}
      <ul className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">{members.data?.map(member => <li key={member.id} className="flex items-center gap-3 rounded-xl border border-slate-200 bg-white p-4"><Avatar member={member} /><span className="min-w-0 break-words font-medium">{member.nickname}</span></li>)}</ul>
    </>}
  </>
}

function Avatar({ member }: { member: GroupMember }) {
  return member.avatarUrl ? <img src={member.avatarUrl} alt="" className="size-10 shrink-0 rounded-full object-cover" />
    : <span aria-hidden className="grid size-10 shrink-0 place-items-center rounded-full bg-indigo-50 font-semibold text-indigo-600">{member.nickname.slice(0, 1).toUpperCase()}</span>
}

function GroupModal({ title, children, onClose, busy = false }: { title: string; children: ReactNode; onClose: () => void; busy?: boolean }) {
  const { t } = useTranslation()
  const ref = useRef<HTMLDialogElement>(null)
  const titleId = useId()
  useEffect(() => { const dialog = ref.current; dialog?.showModal(); return () => dialog?.close() }, [])
  return <dialog ref={ref} aria-labelledby={titleId} onCancel={event => { event.preventDefault(); if (!busy) onClose() }}
    className="m-auto max-h-[85dvh] w-[calc(100%-2rem)] max-w-xl overflow-y-auto rounded-xl border border-slate-200 bg-white p-5 shadow-xl backdrop:bg-slate-900/40 sm:p-6">
    <div className="mb-5 flex items-center justify-between gap-3"><h2 id={titleId} className="text-lg font-semibold break-words">{title}</h2><Button variant="ghost" className="size-9 shrink-0 p-0" disabled={busy} onClick={onClose} aria-label={t('common.close')}><X className="size-4" aria-hidden /></Button></div>
    {children}
  </dialog>
}

function GroupEditor({ group, onClose }: { group?: StudyGroup; onClose: () => void }) {
  const { t } = useTranslation()
  const action = useGroupAction()
  const [name, setName] = useState(group?.name ?? '')
  const [description, setDescription] = useState(group?.description ?? '')
  const descriptionId = useId()
  return <GroupModal title={group ? t('groups.editTitle') : t('groups.newTitle')} onClose={onClose} busy={action.isPending}>
    <form className="space-y-4" onSubmit={event => { event.preventDefault(); action.mutate({ kind: 'save', id: group?.id, name, description }, { onSuccess: onClose }) }}>
      <FormField label={t('groups.name')} value={name} onChange={e => setName(e.target.value)} required maxLength={120} autoFocus disabled={action.isPending} />
      <div><label htmlFor={descriptionId} className="mb-1.5 block font-medium text-slate-700">{t('groups.shortDescription')}</label><textarea id={descriptionId} value={description} onChange={e => setDescription(e.target.value)} rows={3} maxLength={500} disabled={action.isPending} className="w-full rounded-xl border border-slate-300 bg-white p-3 focus:border-indigo-500 focus:outline-none focus:ring-2 focus:ring-indigo-500/20" /></div>
      {action.error && <Alert title={t('groups.saveFailed')}>{action.error.message}</Alert>}
      <div className="flex justify-end gap-2"><Button variant="secondary" disabled={action.isPending} onClick={onClose}>{t('common.cancel')}</Button><Button type="submit" loading={action.isPending} disabled={!name.trim()}>{t('common.save')}</Button></div>
    </form>
  </GroupModal>
}

function MemberManager({ group, onClose }: { group: StudyGroup; onClose: () => void }) {
  const { t } = useTranslation()
  const members = useGroupMembers(group.id)
  const [term, setTerm] = useState('')
  const [query, setQuery] = useState('')
  const [selected, setSelected] = useState<string[]>([])
  const search = useGroupUserSearch(group.id, query)
  const action = useGroupAction()
  const confirm = useConfirm()
  const searchId = useId()
  useEffect(() => { const timer = setTimeout(() => setQuery(term.trim()), 250); return () => clearTimeout(timer) }, [term])
  return <GroupModal title={`${group.name} · ${t('groups.membersTitle')}`} onClose={onClose} busy={action.isPending}>
    <label htmlFor={searchId} className="mb-2 block text-sm font-medium">{t('groups.searchLabel')}</label>
    <input id={searchId} type="search" autoFocus value={term} onChange={e => setTerm(e.target.value)} maxLength={24} placeholder={t('groups.searchPlaceholder')} className="h-11 w-full rounded-xl border border-slate-300 bg-white px-3 focus:border-indigo-500 focus:outline-none focus:ring-2 focus:ring-indigo-500/20" />
    {query.length >= 2 && <div className="mt-3 space-y-2">
      {(search.isPending || search.isFetching) && <p role="status" className="text-sm text-slate-500">{t('friends.searching')}</p>}
      {search.error && <Alert title={t('friends.searchFailed')}>{search.error.message}</Alert>}
      {search.data?.length === 0 && <p className="text-sm text-slate-500">{t('friends.noUser')}</p>}
      <ul className="max-h-56 space-y-2 overflow-y-auto">{search.data?.map(person => <li key={person.id}>
        <label className="flex cursor-pointer items-center gap-3 rounded-xl border border-slate-200 p-3"><input type="checkbox" aria-label={t('groups.select', { name: person.nickname })} checked={person.isMember || selected.includes(person.id)} disabled={person.isMember || action.isPending || (!selected.includes(person.id) && selected.length >= 100)}
          onChange={e => setSelected(ids => e.target.checked ? [...ids, person.id] : ids.filter(id => id !== person.id))} className="size-4 accent-indigo-600" />
          <Avatar member={person} /><span className="min-w-0 flex-1 break-words text-sm font-medium">{person.nickname}</span>{person.isMember && <span className="text-xs text-slate-500">{t('groups.isMember')}</span>}
        </label>
      </li>)}</ul>
    </div>}
    <div className="mt-4 flex items-center justify-between gap-3"><p className="text-xs text-slate-500" role="status">{t('groups.selected', { count: selected.length })}</p><Button disabled={!selected.length || action.isPending} onClick={() => action.mutate({ kind: 'add', id: group.id, userIds: selected }, { onSuccess: () => setSelected([]) })}>{t('groups.addSelected')}</Button></div>
    {action.error && <div className="mt-4"><Alert title={t('profile.actionFailed')}>{action.error.message}</Alert></div>}
    <h3 className="mt-6 border-t border-slate-200 pt-4 font-semibold">{t('groups.membersCount', { n: members.data?.length ?? 0 })}</h3>
    {members.isPending && <p role="status" className="mt-3 text-slate-500">{t('common.loadingDots')}</p>}
    {members.error && <Alert title={t('groups.membersFailed')}>{members.error.message}</Alert>}
    {members.data?.length === 0 && <p className="mt-3 text-sm text-slate-500">{t('groups.noMembersShort')}</p>}
    <ul className="mt-3 max-h-72 space-y-2 overflow-y-auto">{members.data?.map(member => <li key={member.id} className="flex items-center gap-3 rounded-xl bg-slate-50 p-3"><Avatar member={member} /><span className="min-w-0 flex-1 break-words text-sm font-medium">{member.nickname}</span>
      <Button variant="ghost" className="h-8 px-2 text-xs text-rose-600" disabled={action.isPending} onClick={async () => {
        if (await confirm({ title: t('groups.removeTitle'), description: t('groups.removeText', { name: member.nickname }), confirmLabel: t('friends.removeConfirm'), danger: true })) action.mutate({ kind: 'remove', id: group.id, userId: member.id })
      }}>{t('friends.removeConfirm')}</Button>
    </li>)}</ul>
  </GroupModal>
}
