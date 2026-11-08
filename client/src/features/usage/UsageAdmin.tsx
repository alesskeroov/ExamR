import { useEffect, useRef, useState } from 'react'
import { Alert } from '../../components/common/Alert'
import { Button } from '../../components/common/Button'
import { EmptyState } from '../../components/common/EmptyState'
import { useAdminUsage, useUpdateUsage, type AdminUsage, type UsageLimits } from './api'

const fields = [ ['monthlyUploads', 'Sənəd'], ['monthlyExams', 'İmtahan'], ['monthlyBattles', 'Battle'], ['monthlyAiRequests', 'AI sorğusu'] ] as const

export function UsageAdmin() {
  const [query, setQuery] = useState('')
  const [term, setTerm] = useState('')
  const [page, setPage] = useState(1)
  const [selected, setSelected] = useState<AdminUsage | null>(null)
  const users = useAdminUsage(query, page)
  useEffect(() => { const timer = setTimeout(() => { setQuery(term.trim()); setPage(1) }, 250); return () => clearTimeout(timer) }, [term])
  return <section className="space-y-4">
    <label className="block text-sm font-medium">İstifadəçi axtar
      <input type="search" maxLength={256} value={term} onChange={e => setTerm(e.target.value)} placeholder="Nickname və ya email" className="mt-2 h-11 w-full rounded-xl border border-slate-300 px-3" />
    </label>
    {users.isPending && <div role="status" className="h-32 animate-pulse rounded-xl bg-slate-100" aria-label="Yüklənir" />}
    {users.error && <Alert title="İstifadəçilər yüklənmədi">{users.error.message}</Alert>}
    {users.data?.items.length === 0 && <EmptyState title="İstifadəçi tapılmadı" />}
    {!!users.data?.items.length && <div className="overflow-x-auto rounded-xl border border-slate-200"><table className="w-full min-w-[640px] text-left text-sm">
      <thead className="bg-slate-50"><tr><th className="p-3">İstifadəçi</th>{fields.map(([key, label]) => <th key={key} className="p-3">{label}</th>)}<th className="p-3">Limitlər</th></tr></thead>
      <tbody>{users.data.items.map(person => <tr key={person.id} className="border-t border-slate-100">
        <td className="p-3"><p className="font-medium">{person.nickname}{person.isAdmin ? ' · Admin' : ''}</p><p className="text-xs text-slate-500">{person.email}</p></td>
        {person.usage.items.map(item => <td key={item.kind} className="whitespace-nowrap p-3">{item.used} / {item.limit ?? '∞'}</td>)}
        <td className="p-3"><Button variant="secondary" onClick={() => setSelected(person)}>Redaktə et</Button></td>
      </tr>)}</tbody>
    </table></div>}
    {users.data && <div className="flex items-center gap-3"><Button variant="secondary" disabled={page === 1} onClick={() => setPage(p => p - 1)}>Əvvəlki</Button><span>{page}</span><Button variant="secondary" disabled={page * 20 >= users.data.totalCount} onClick={() => setPage(p => p + 1)}>Növbəti</Button></div>}
    {selected && <LimitDialog key={selected.id} person={selected} close={() => setSelected(null)} />}
  </section>
}

function LimitDialog({ person, close }: { person: AdminUsage; close: () => void }) {
  const dialog = useRef<HTMLDialogElement>(null)
  const update = useUpdateUsage()
  const [limits, setLimits] = useState<UsageLimits>(person.custom ?? { monthlyUploads: null, monthlyExams: null, monthlyBattles: null, monthlyAiRequests: null, unlimited: false })
  useEffect(() => { const el = dialog.current!; el.showModal(); return () => el.close() }, [])
  const save = (value: UsageLimits | null) => update.mutate({ id: person.id, limits: value }, { onSuccess: close })
  return <dialog ref={dialog} onCancel={e => { if (update.isPending) e.preventDefault(); else close() }} aria-labelledby="usage-dialog-title" className="m-auto w-[min(30rem,calc(100%-2rem))] rounded-xl bg-white p-6 text-slate-900 shadow-xl backdrop:bg-slate-950/40">
    <form onSubmit={e => { e.preventDefault(); save(limits) }} className="space-y-4">
      <h2 id="usage-dialog-title" className="text-lg font-semibold">{person.nickname} · aylıq limitlər</h2>
      <p className="text-sm text-slate-500">Boş sahə standart limiti tətbiq edir. 0 əməliyyatı bağlayır.</p>
      {person.isAdmin && <Alert tone="info" title="Admin hesabı limitsizdir">Bu dəyərlər yalnız Admin rolu götürüldükdən sonra tətbiq ediləcək.</Alert>}
      {fields.map(([key, label]) => <label key={key} className="block text-sm font-medium">{label}<input type="number" min={0} max={100000} step={1} disabled={limits.unlimited || update.isPending} value={limits[key] ?? ''} placeholder={String(person.defaults[key])} onChange={e => setLimits({ ...limits, [key]: e.target.value === '' ? null : Number(e.target.value) })} className="mt-1 h-11 w-full rounded-xl border border-slate-300 px-3 disabled:bg-slate-50" /></label>)}
      <label className="flex items-center gap-2"><input type="checkbox" checked={limits.unlimited} disabled={update.isPending} onChange={e => setLimits({ ...limits, unlimited: e.target.checked })} />Limitsiz</label>
      {update.error && <Alert title="Limit saxlanmadı">{update.error.message}</Alert>}
      <div className="flex flex-wrap gap-2"><Button type="submit" disabled={update.isPending}>Yadda saxla</Button><Button type="button" variant="secondary" disabled={update.isPending} onClick={() => save(null)}>Defaulta qaytar</Button><Button type="button" variant="ghost" disabled={update.isPending} onClick={close}>Bağla</Button></div>
    </form>
  </dialog>
}
