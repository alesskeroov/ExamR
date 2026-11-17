import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert } from '../../components/common/Alert'
import { Button } from '../../components/common/Button'
import { EmptyState } from '../../components/common/EmptyState'
import { cn } from '../../lib/utils'
import type { UsageItem } from '../usage/api'
import { AdminDialog } from './AdminDialog'
import { useAdminBillingUser, useAdminBillingUsers, useAdminPlans, useBulkBilling, type AdminBillingUser, type BulkAction, type BulkInput, type BulkResult, type CurrentPlan, type UserStatusFilter } from './api'
import { endsWithin, formatDateTime, formatDay, formatMoney, limitKeys, limitValue, periodLabel } from './format'

const PAGE_SIZE = 25
const usageKinds = ['Upload', 'Exam', 'Battle', 'DimTest'] as const
const statuses: UserStatusFilter[] = ['', 'active', 'expiring', 'expired', 'free', 'pending', 'custom']
const actions: BulkAction[] = ['AssignPlan', 'Extend', 'CancelPlan', 'SetLimits', 'ClearLimits', 'SetUnlimited']

export function BillingUsersAdmin() {
  const { t } = useTranslation()
  const [term, setTerm] = useState('')
  const [query, setQuery] = useState('')
  const [planId, setPlanId] = useState('')
  const [status, setStatus] = useState<UserStatusFilter>('')
  const [page, setPage] = useState(1)
  const [selected, setSelected] = useState<Map<string, string>>(new Map())
  const [action, setAction] = useState<BulkAction | null>(null)
  const [openUser, setOpenUser] = useState<string | null>(null)
  const plans = useAdminPlans()
  const users = useAdminBillingUsers(query, planId, status, page)
  useEffect(() => { const timer = setTimeout(() => { setQuery(term.trim()); setPage(1) }, 250); return () => clearTimeout(timer) }, [term])

  const rows = users.data?.items ?? []
  const allOnPage = rows.length > 0 && rows.every(r => selected.has(r.id))
  const toggle = (person: AdminBillingUser) => setSelected(prev => {
    const next = new Map(prev)
    if (next.has(person.id)) next.delete(person.id); else next.set(person.id, person.nickname)
    return next
  })
  const togglePage = () => setSelected(prev => {
    const next = new Map(prev)
    for (const r of rows) if (allOnPage) next.delete(r.id); else next.set(r.id, r.nickname)
    return next
  })

  return <section className="space-y-4">
    <div className="grid gap-3 md:grid-cols-[1fr_12rem_12rem]">
      <label className="block text-sm font-medium">{t('billingAdmin.search')}
        <input type="search" maxLength={256} value={term} onChange={e => setTerm(e.target.value)} placeholder={t('billingAdmin.searchPlaceholder')} className="mt-1 h-11 w-full rounded-xl border border-slate-300 px-3" />
      </label>
      <label className="block text-sm font-medium">{t('billingAdmin.planFilter')}
        <select value={planId} onChange={e => { setPlanId(e.target.value); setPage(1) }} className="mt-1 h-11 w-full rounded-xl border border-slate-300 bg-white px-3">
          <option value="">{t('billingAdmin.allPlans')}</option>
          {plans.data?.map(({ plan }) => <option key={plan.id} value={plan.id}>{plan.name}</option>)}
        </select>
      </label>
      <label className="block text-sm font-medium">{t('billingAdmin.statusFilter')}
        <select value={status} onChange={e => { setStatus(e.target.value as UserStatusFilter); setPage(1) }} className="mt-1 h-11 w-full rounded-xl border border-slate-300 bg-white px-3">
          {statuses.map(s => <option key={s} value={s}>{t(`billingAdmin.status.${s || 'all'}`)}</option>)}
        </select>
      </label>
    </div>

    {/* Always rendered, so selecting a row never pushes the table down under the pointer. */}
    <div role="region" aria-label={t('billingAdmin.bulkAria')} className={cn('sticky top-2 z-10 flex flex-wrap items-center gap-2 rounded-xl border p-3',
      selected.size > 0 ? 'border-indigo-200 bg-indigo-50 shadow-sm' : 'border-slate-200 bg-white')}>
      <span className={cn('mr-2 text-sm font-medium', selected.size > 0 ? 'text-indigo-800' : 'text-slate-500')}>
        {selected.size > 0 ? t('billingAdmin.selected', { count: selected.size }) : t('billingAdmin.selectHint')}
      </span>
      {actions.map(a => <Button key={a} variant="secondary" disabled={selected.size === 0} onClick={() => setAction(a)}>{t(`billingAdmin.action.${a}`)}</Button>)}
      {selected.size > 0 && <Button variant="ghost" onClick={() => setSelected(new Map())}>{t('billingAdmin.clearSelection')}</Button>}
    </div>

    {users.isPending && <div role="status" className="h-40 animate-pulse rounded-xl bg-slate-100" aria-label={t('common.loading')} />}
    {users.isError && <Alert title={t('billingAdmin.loadFailed')}>{users.error.message}</Alert>}
    {users.data && rows.length === 0 && <EmptyState title={t('billingAdmin.noUsers')} />}
    {rows.length > 0 && <div className="relative overflow-x-auto rounded-xl border border-slate-200 bg-white">
      <table className="w-full min-w-[860px] text-left text-sm">
        <thead className="bg-slate-50 text-slate-600"><tr>
          <th className="w-10 p-3"><input type="checkbox" checked={allOnPage} onChange={togglePage} aria-label={t('billingAdmin.selectPage')} /></th>
          <th className="p-3">{t('common.user')}</th>
          <th className="p-3">{t('billingAdmin.plan')}</th>
          {usageKinds.map(k => <th key={k} className="whitespace-nowrap p-3">{t(`usage.strip.${k}`)}</th>)}
          <th className="p-3"><span className="sr-only">{t('billingAdmin.flags')}</span></th>
        </tr></thead>
        <tbody>{rows.map(person => <tr key={person.id} className={cn('border-t border-slate-100', selected.has(person.id) && 'bg-indigo-50/50')}>
          <td className="p-3"><input type="checkbox" checked={selected.has(person.id)} onChange={() => toggle(person)} aria-label={t('billingAdmin.selectUser', { name: person.nickname })} /></td>
          <td className="p-3">
            <button type="button" onClick={() => setOpenUser(person.id)} className="text-left font-medium text-indigo-700 hover:underline">{person.nickname}</button>
            {person.isAdmin && <span className="ml-1 text-xs text-slate-500">· Admin</span>}
            <p className="text-xs text-slate-500">{person.email}</p>
          </td>
          <td className="p-3"><PlanCell plan={person.plan} /></td>
          {usageKinds.map(k => <td key={k} className="whitespace-nowrap p-3"><UsageCell item={person.usage.items.find(i => i.kind === k)} /></td>)}
          <td className="p-3"><div className="flex flex-wrap gap-1">
            {person.hasPending && <span className="rounded-full bg-amber-50 px-2 py-0.5 text-xs text-amber-700">{t('billingAdmin.pendingFlag')}</span>}
            {person.custom && <span className="rounded-full bg-slate-100 px-2 py-0.5 text-xs text-slate-700">{person.custom.unlimited ? t('billingAdmin.unlimitedFlag') : t('billingAdmin.customFlag')}</span>}
          </div></td>
        </tr>)}</tbody>
      </table>
    </div>}
    {users.data && users.data.totalCount > PAGE_SIZE && <div className="flex items-center gap-3">
      <Button variant="secondary" disabled={page === 1} onClick={() => setPage(p => p - 1)}>{t('common.previous')}</Button>
      <span className="text-sm">{page} / {Math.ceil(users.data.totalCount / PAGE_SIZE)}</span>
      <Button variant="secondary" disabled={page * PAGE_SIZE >= users.data.totalCount} onClick={() => setPage(p => p + 1)}>{t('common.next')}</Button>
    </div>}

    {action && <BulkDialog action={action} users={[...selected.entries()].map(([id, nickname]) => ({ id, nickname }))}
      onClose={() => setAction(null)} onDone={() => setSelected(new Map())} />}
    {openUser && <UserDialog id={openUser} onClose={() => setOpenUser(null)} />}
  </section>
}

function PlanCell({ plan }: { plan: CurrentPlan }) {
  const { t } = useTranslation()
  const soon = plan.endsAt && endsWithin(plan.endsAt, 7)
  return <div>
    <p className={cn('font-medium', plan.isFree ? 'text-slate-500' : 'text-slate-900')}>{plan.name}</p>
    {plan.endsAt && <p className={cn('text-xs', soon ? 'text-amber-700' : 'text-slate-500')}>{t('billing.until', { date: formatDay(plan.endsAt) })}</p>}
    {plan.next && <p className="text-xs text-slate-500">{t('billing.nextPlan', { name: plan.next.name, date: formatDay(plan.next.startsAt) })}</p>}
  </div>
}

function UsageCell({ item }: { item: UsageItem | undefined }) {
  if (!item) return null
  const full = item.limit !== null && item.used >= item.limit
  return <span className={cn('tabular-nums', full && 'font-medium text-rose-700')}>{item.used} / {limitValue(item.limit)}</span>
}

type BulkForm = { planId: string; periods: number; days: string; note: string; limits: Record<(typeof limitKeys)[number], string> }

function BulkDialog({ action, users, onClose, onDone }: { action: BulkAction; users: { id: string; nickname: string }[]; onClose: () => void; onDone: () => void }) {
  const { t } = useTranslation()
  const plans = useAdminPlans()
  const bulk = useBulkBilling()
  const paid = plans.data?.filter(p => !p.plan.isFree) ?? []
  const [form, setForm] = useState<BulkForm>({ planId: '', periods: 1, days: '', note: '', limits: { monthlyUploads: '', monthlyExams: '', monthlyBattles: '', monthlyDimTests: '' } })
  const [result, setResult] = useState<BulkResult | null>(null)
  const plan = paid.find(p => p.plan.id === form.planId)?.plan
  const names = users.slice(0, 5).map(u => u.nickname).join(', ') + (users.length > 5 ? ` +${users.length - 5}` : '')
  const num = (v: string) => v === '' ? undefined : Number(v)
  const valid = action === 'AssignPlan' ? !!plan : action === 'Extend' ? Number(form.days) >= 1 : true

  const submit = () => {
    const input: BulkInput = { userIds: users.map(u => u.id), action, note: form.note.trim() || undefined }
    if (action === 'AssignPlan') Object.assign(input, { planId: form.planId, periods: form.periods, days: num(form.days) })
    if (action === 'Extend') input.days = Number(form.days)
    if (action === 'SetLimits') for (const key of limitKeys) input[key] = form.limits[key] === '' ? null : Number(form.limits[key])
    bulk.mutate(input, { onSuccess: r => { setResult(r); onDone() } })
  }

  if (result) return <AdminDialog title={t('billingAdmin.resultTitle')} onClose={onClose}>
    <p className="text-sm">{t('billingAdmin.resultText', { updated: result.updated, skipped: result.skipped })}</p>
    {result.skipped > 0 && <ul className="mt-3 max-h-48 list-disc space-y-1 overflow-y-auto pl-5 text-sm text-slate-600">
      {result.items.filter(i => !i.updated).map(i => <li key={i.userId}>{users.find(u => u.id === i.userId)?.nickname}: {i.reason}</li>)}
    </ul>}
    <Button className="mt-4" onClick={onClose}>{t('common.close')}</Button>
  </AdminDialog>

  return <AdminDialog title={t(`billingAdmin.action.${action}`)} busy={bulk.isPending} onClose={onClose}>
    <form onSubmit={e => { e.preventDefault(); if (valid) submit() }} className="space-y-4">
      <p className="text-sm text-slate-600">{t('billingAdmin.forUsers', { count: users.length, names })}</p>
      {action === 'AssignPlan' && <>
        <label className="block text-sm font-medium">{t('billingAdmin.plan')}
          <select required value={form.planId} onChange={e => setForm({ ...form, planId: e.target.value })} className="mt-1 h-11 w-full rounded-xl border border-slate-300 bg-white px-3">
            <option value="">{t('billingAdmin.choosePlan')}</option>
            {paid.map(({ plan: p }) => <option key={p.id} value={p.id}>{p.name} · {formatMoney(p.price)} / {t('billing.days', { count: p.durationDays })}</option>)}
          </select>
        </label>
        <div className="grid grid-cols-2 gap-3">
          <label className="block text-sm font-medium">{t('billingAdmin.periods')}
            <input type="number" min={1} max={12} value={form.periods} onChange={e => setForm({ ...form, periods: Math.min(12, Math.max(1, Number(e.target.value) || 1)) })} className="mt-1 h-11 w-full rounded-xl border border-slate-300 px-3" />
            {plan && <span className="mt-1 block text-xs font-normal text-slate-500">= {periodLabel(plan, form.periods)}</span>}
          </label>
          <label className="block text-sm font-medium">{t('billingAdmin.extraDays')}
            <input type="number" min={0} max={365} value={form.days} placeholder="0" onChange={e => setForm({ ...form, days: e.target.value })} className="mt-1 h-11 w-full rounded-xl border border-slate-300 px-3" />
          </label>
        </div>
        {plan && <p className="text-sm text-slate-600">{t('billingAdmin.assignSummary', { count: users.length, name: plan.name, days: plan.durationDays * form.periods + (Number(form.days) || 0) })}</p>}
        <Alert tone="info" title={t('billingAdmin.assignNoteTitle')}>{t('billingAdmin.assignNote')}</Alert>
      </>}
      {action === 'Extend' && <label className="block text-sm font-medium">{t('billingAdmin.extendDays')}
        <input type="number" required min={1} max={365} value={form.days} onChange={e => setForm({ ...form, days: e.target.value })} className="mt-1 h-11 w-full rounded-xl border border-slate-300 px-3" />
      </label>}
      {action === 'SetLimits' && <>
        <p className="text-sm text-slate-500">{t('billingAdmin.limitsHint')}</p>
        <div className="grid grid-cols-2 gap-3">{limitKeys.map(key => <label key={key} className="block text-sm font-medium">{t(`billing.limit.${key}`)}
          <input type="number" min={0} max={100000} value={form.limits[key]} placeholder={t('billingAdmin.fromPlan')} onChange={e => setForm({ ...form, limits: { ...form.limits, [key]: e.target.value } })} className="mt-1 h-11 w-full rounded-xl border border-slate-300 px-3" />
        </label>)}</div>
      </>}
      {(action === 'CancelPlan' || action === 'ClearLimits' || action === 'SetUnlimited') && <p className="text-sm">{t(`billingAdmin.confirm.${action}`, { count: users.length })}</p>}
      {(action === 'AssignPlan' || action === 'Extend' || action === 'CancelPlan') && <label className="block text-sm font-medium">{t('billingAdmin.note')}
        <input maxLength={500} value={form.note} onChange={e => setForm({ ...form, note: e.target.value })} className="mt-1 h-11 w-full rounded-xl border border-slate-300 px-3" />
      </label>}
      {bulk.isError && <Alert title={t('billingAdmin.bulkFailed')}>{bulk.error.message}</Alert>}
      <div className="flex flex-wrap gap-2">
        <Button type="submit" disabled={!valid} loading={bulk.isPending}>{t('billingAdmin.apply', { count: users.length })}</Button>
        <Button type="button" variant="ghost" disabled={bulk.isPending} onClick={onClose}>{t('common.cancel')}</Button>
      </div>
    </form>
  </AdminDialog>
}

function UserDialog({ id, onClose }: { id: string; onClose: () => void }) {
  const { t } = useTranslation()
  const detail = useAdminBillingUser(id)
  const [action, setAction] = useState<BulkAction | null>(null)
  const person = detail.data?.user
  return <AdminDialog wide title={person?.nickname ?? t('common.loading')} onClose={onClose}>
    {detail.isPending && <div role="status" className="h-40 animate-pulse rounded-xl bg-slate-100" aria-label={t('common.loading')} />}
    {detail.isError && <Alert title={t('billingAdmin.loadFailed')}>{detail.error.message}</Alert>}
    {person && detail.data && <div className="space-y-5 text-sm">
      <p className="text-slate-500">{person.email}{person.isAdmin && ' · Admin'}</p>
      <section className="grid gap-4 sm:grid-cols-2">
        <div className="rounded-xl border border-slate-200 p-4"><h3 className="font-semibold">{t('billingAdmin.plan')}</h3><div className="mt-2"><PlanCell plan={person.plan} /></div></div>
        <div className="rounded-xl border border-slate-200 p-4"><h3 className="font-semibold">{t('billingAdmin.thisMonth')}</h3>
          <dl className="mt-2 grid grid-cols-2 gap-x-4 gap-y-1">{usageKinds.map(k => <div key={k} className="contents"><dt className="text-slate-500">{t(`usage.strip.${k}`)}</dt><dd><UsageCell item={person.usage.items.find(i => i.kind === k)} /></dd></div>)}</dl>
          {person.custom && <p className="mt-2 text-xs text-slate-500">{person.custom.unlimited ? t('billingAdmin.customUnlimited') : t('billingAdmin.customActive')}</p>}
        </div>
      </section>
      <div className="flex flex-wrap gap-2">{actions.map(a => <Button key={a} variant="secondary" onClick={() => setAction(a)}>{t(`billingAdmin.action.${a}`)}</Button>)}</div>
      <section><h3 className="font-semibold">{t('billingAdmin.periodsHistory')}</h3>
        {detail.data.history.subscriptions.length === 0 ? <p className="mt-1 text-slate-500">{t('billingAdmin.noHistory')}</p> :
          <ul className="mt-2 divide-y divide-slate-100 rounded-xl border border-slate-200">{detail.data.history.subscriptions.map(s => <li key={s.id} className="flex flex-wrap justify-between gap-2 px-3 py-2">
            <span><span className="font-medium">{s.planName}</span> · {formatDay(s.startsAt)} – {formatDay(s.endsAt)}{s.cancelledAt && <span className="text-rose-700"> · {t('billingAdmin.cancelled')}</span>}</span>
            <span className="text-slate-500">{t(`billingAdmin.source.${s.source}`)}{s.note && ` · ${s.note}`}</span>
          </li>)}</ul>}
      </section>
      <section><h3 className="font-semibold">{t('billingAdmin.paymentsHistory')}</h3>
        {detail.data.history.payments.length === 0 ? <p className="mt-1 text-slate-500">{t('billingAdmin.noHistory')}</p> :
          <ul className="mt-2 divide-y divide-slate-100 rounded-xl border border-slate-200">{detail.data.history.payments.map(p => <li key={p.id} className="flex flex-wrap justify-between gap-2 px-3 py-2">
            <span><span className="font-mono text-xs">{p.code}</span> · {p.planName} · {formatMoney(p.amount)}</span>
            <span className="text-slate-500">{formatDateTime(p.createdAt)} · {t(`billing.status.${p.status}`)}</span>
          </li>)}</ul>}
      </section>
      <Button variant="ghost" onClick={onClose}>{t('common.close')}</Button>
    </div>}
    {action && person && <BulkDialog action={action} users={[{ id: person.id, nickname: person.nickname }]} onClose={() => setAction(null)} onDone={() => undefined} />}
  </AdminDialog>
}
