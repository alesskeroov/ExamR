import { useEffect, useId, useRef, useState } from 'react'
import { X } from 'lucide-react'
import { Alert } from '../../components/common/Alert'
import { Button } from '../../components/common/Button'
import { useGroups } from '../groups/api'
import { useSaveSharing, useSharing, type SharingSettings } from './sharing-api'

export function DocumentSharingModal({ documentId, name, onClose }: { documentId: string; name: string; onClose: () => void }) {
  const ref = useRef<HTMLDialogElement>(null)
  const titleId = useId()
  const settings = useSharing(documentId)
  const [busy, setBusy] = useState(false)
  useEffect(() => { const dialog = ref.current; dialog?.showModal(); return () => dialog?.close() }, [])
  return <dialog ref={ref} aria-labelledby={titleId} onCancel={event => { event.preventDefault(); if (!busy) onClose() }} className="m-auto max-h-[85dvh] w-[calc(100%-2rem)] max-w-xl overflow-y-auto rounded-xl border border-slate-200 bg-white p-6 shadow-xl backdrop:bg-slate-900/40">
    <div className="mb-2 flex items-center justify-between gap-3"><h2 id={titleId} className="text-lg font-semibold">Sənəd paylaşımı</h2><Button variant="ghost" className="size-9 p-0" disabled={busy} aria-label="Bağla" onClick={onClose}><X className="size-4" aria-hidden /></Button></div>
    <p className="mb-5 break-words text-sm text-slate-500">{name}</p>
    {settings.isPending && <p role="status">Paylaşım yüklənir…</p>}
    {settings.error && <Alert title="Paylaşım açılmadı">{settings.error.message}</Alert>}
    {settings.data && <SharingForm documentId={documentId} initial={settings.data} onClose={onClose} setBusy={setBusy} />}
  </dialog>
}

function SharingForm({ documentId, initial, onClose, setBusy }: { documentId: string; initial: SharingSettings; onClose: () => void; setBusy: (value: boolean) => void }) {
  const [mode, setMode] = useState(initial.shareWithEveryone ? 'public' : 'groups')
  const [groupIds, setGroupIds] = useState(initial.groupIds)
  const radioName = useId()
  const groups = useGroups(true)
  const save = useSaveSharing(documentId)
  useEffect(() => { setBusy(save.isPending); return () => setBusy(false) }, [save.isPending, setBusy])
  return <form className="space-y-5" onSubmit={event => {
    event.preventDefault()
    // Legacy direct user grants are kept as-is; the UI only manages group and public sharing.
    save.mutate({ shareWithEveryone: mode === 'public', userIds: initial.users.map(user => user.id), groupIds: mode === 'groups' ? groupIds : [] }, { onSuccess: onClose })
  }}>
    <fieldset disabled={save.isPending} className="space-y-2"><legend className="mb-2 text-sm font-medium">Kimlər görə bilər?</legend>{[
      ['groups', 'Qruplar üçün'], ['public', 'Bütün istifadəçilər üçün'],
    ].map(([value, label]) => <label key={value} className="flex cursor-pointer items-center gap-3 rounded-xl border border-slate-200 p-3 text-sm"><input type="radio" name={radioName} checked={mode === value} onChange={() => setMode(value)} className="accent-indigo-600" />{label}</label>)}</fieldset>
    {mode === 'groups' && <>
      <fieldset disabled={save.isPending}><legend className="mb-2 text-sm font-medium">Qruplar</legend>
        {groups.isPending && <p role="status" className="text-sm text-slate-500">Qruplar yüklənir…</p>}
        {groups.error && <Alert title="Qruplar yüklənmədi">{groups.error.message}</Alert>}
        {groups.data?.length === 0 && <p className="text-sm text-slate-500">Hələ qrup yoxdur. Admin Panelin Qruplar bölməsindən yaradın.</p>}
        <div className="max-h-40 space-y-2 overflow-y-auto">{groups.data?.map(group => <label key={group.id} className="flex items-center gap-2 rounded-lg bg-slate-50 p-2.5 text-sm"><input type="checkbox" checked={groupIds.includes(group.id)} disabled={!groupIds.includes(group.id) && groupIds.length >= 100} onChange={event => setGroupIds(ids => event.target.checked ? [...ids, group.id] : ids.filter(id => id !== group.id))} className="accent-indigo-600" /><span className="min-w-0 flex-1 break-words">{group.name}</span><span className="text-xs text-slate-500">{group.memberCount} üzv</span></label>)}</div>
      </fieldset>
      <p className="text-xs leading-5 text-slate-500">Bir neçə qrup seçə bilərsiniz. Qrup üzvlüyü dəyişdikdə sənədə giriş də avtomatik dəyişir.</p>
    </>}
    {save.error && <Alert title="Paylaşım saxlanmadı">{save.error.message}</Alert>}
    <div className="flex justify-end gap-2"><Button variant="secondary" disabled={save.isPending} onClick={onClose}>Ləğv et</Button><Button type="submit" loading={save.isPending} disabled={mode === 'groups' && groupIds.length === 0}>Saxla</Button></div>
  </form>
}
