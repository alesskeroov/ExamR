import { useState } from 'react'
import { Plus } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Alert } from '../../components/common/Alert'
import { Button } from '../../components/common/Button'
import { useConfirm } from '../../lib/use-confirm'
import { useAdminPlans, useSavePlan, type Plan, type PlanInput } from './api'
import { limitKeys } from './format'

type Draft = Omit<PlanInput, 'price' | 'durationDays' | 'sortOrder' | (typeof limitKeys)[number]> & {
  price: string; durationDays: string; sortOrder: string
  limits: Record<(typeof limitKeys)[number], { value: string; unlimited: boolean }>
}

const toDraft = (plan: Plan | null, nextOrder: number): Draft => ({
  name: plan?.name ?? '', description: plan?.description ?? '', price: String(plan?.price ?? ''), durationDays: String(plan?.durationDays ?? 30),
  isActive: plan?.isActive ?? true, isFeatured: plan?.isFeatured ?? false, sortOrder: String(plan?.sortOrder ?? nextOrder),
  limits: Object.fromEntries(limitKeys.map(key => [key, { value: plan && plan[key] !== null ? String(plan[key]) : '0', unlimited: !!plan && plan[key] === null }])) as Draft['limits'],
})

export function BillingPlansAdmin() {
  const { t } = useTranslation()
  const plans = useAdminPlans()
  const [adding, setAdding] = useState(false)
  if (plans.isPending) return <div role="status" className="h-40 animate-pulse rounded-xl bg-slate-100" aria-label={t('common.loading')} />
  if (plans.isError) return <Alert title={t('billingAdmin.loadFailed')}>{plans.error.message}</Alert>
  const nextOrder = Math.max(0, ...plans.data.map(p => p.plan.sortOrder)) + 1
  return <section className="space-y-4">
    <p className="text-sm text-slate-600">{t('billingAdmin.plansHint')}</p>
    <ul className="grid gap-4 lg:grid-cols-2">
      {plans.data.map(({ plan, activeUsers }) => <li key={plan.id}><PlanEditor plan={plan} activeUsers={activeUsers} nextOrder={nextOrder} /></li>)}
      {adding && <li><PlanEditor plan={null} activeUsers={0} nextOrder={nextOrder} onDone={() => setAdding(false)} /></li>}
    </ul>
    {!adding && <Button variant="secondary" onClick={() => setAdding(true)}><Plus className="size-4" aria-hidden />{t('billingAdmin.newPlan')}</Button>}
  </section>
}

function PlanEditor({ plan, activeUsers, nextOrder, onDone }: { plan: Plan | null; activeUsers: number; nextOrder: number; onDone?: () => void }) {
  const { t } = useTranslation()
  const save = useSavePlan()
  const confirm = useConfirm()
  const [draft, setDraft] = useState(() => toDraft(plan, nextOrder))
  const [saved, setSaved] = useState(false)
  const isFree = plan?.isFree ?? false
  const set = (patch: Partial<Draft>) => { setDraft({ ...draft, ...patch }); setSaved(false) }

  const submit = async () => {
    if (plan && activeUsers > 0 && !await confirm({ title: t('billingAdmin.saveWarnTitle'), description: t('billingAdmin.saveWarn', { count: activeUsers, name: plan.name }), confirmLabel: t('questions.saveButton') })) return
    const input: PlanInput = {
      name: draft.name.trim(), description: draft.description?.trim() || null, price: isFree ? 0 : Number(draft.price), durationDays: Number(draft.durationDays),
      isActive: isFree || draft.isActive, isFeatured: draft.isFeatured, sortOrder: Number(draft.sortOrder) || 0,
      monthlyUploads: null, monthlyExams: null, monthlyBattles: null, monthlyDimTests: null,
    }
    for (const key of limitKeys) input[key] = draft.limits[key].unlimited ? null : Number(draft.limits[key].value) || 0
    save.mutate({ id: plan?.id ?? null, plan: input }, { onSuccess: () => { setSaved(true); onDone?.() } })
  }

  return <form onSubmit={e => { e.preventDefault(); void submit() }} className="space-y-3 rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
    <div className="flex flex-wrap items-center justify-between gap-2">
      <h3 className="font-semibold">{plan?.name || t('billingAdmin.newPlan')}</h3>
      {plan && <span className="text-xs text-slate-500">{t('billingAdmin.activeUsers', { count: activeUsers })}</span>}
    </div>
    <div className="grid gap-3 sm:grid-cols-2">
      <label className="block text-sm font-medium">{t('billingAdmin.planName')}
        <input required maxLength={40} value={draft.name} onChange={e => set({ name: e.target.value })} className="mt-1 h-10 w-full rounded-xl border border-slate-300 px-3" />
      </label>
      <label className="block text-sm font-medium">{t('billingAdmin.sortOrder')}
        <input type="number" min={0} max={1000} value={draft.sortOrder} onChange={e => set({ sortOrder: e.target.value })} className="mt-1 h-10 w-full rounded-xl border border-slate-300 px-3" />
      </label>
      <label className="block text-sm font-medium sm:col-span-2">{t('billingAdmin.planDescription')}
        <input maxLength={300} value={draft.description ?? ''} onChange={e => set({ description: e.target.value })} className="mt-1 h-10 w-full rounded-xl border border-slate-300 px-3" />
      </label>
      {!isFree && <>
        <label className="block text-sm font-medium">{t('billingAdmin.price')}
          <input type="number" required min={0} max={10000} step={0.01} value={draft.price} onChange={e => set({ price: e.target.value })} className="mt-1 h-10 w-full rounded-xl border border-slate-300 px-3" />
        </label>
        <label className="block text-sm font-medium">{t('billingAdmin.duration')}
          <input type="number" required min={1} max={365} value={draft.durationDays} onChange={e => set({ durationDays: e.target.value })} className="mt-1 h-10 w-full rounded-xl border border-slate-300 px-3" />
        </label>
      </>}
    </div>
    <fieldset className="grid gap-3 sm:grid-cols-2">
      <legend className="mb-1 text-sm font-medium">{t('billingAdmin.monthlyLimits')}</legend>
      {limitKeys.map(key => <div key={key} className="text-sm">
        <label className="block font-medium">{t(`billing.limit.${key}`)}
          <input type="number" min={0} max={100000} disabled={draft.limits[key].unlimited} value={draft.limits[key].value}
            onChange={e => set({ limits: { ...draft.limits, [key]: { ...draft.limits[key], value: e.target.value } } })} className="mt-1 h-10 w-full rounded-xl border border-slate-300 px-3 disabled:bg-slate-50" />
        </label>
        <label className="mt-1 flex items-center gap-2 text-slate-600"><input type="checkbox" checked={draft.limits[key].unlimited}
          onChange={e => set({ limits: { ...draft.limits, [key]: { ...draft.limits[key], unlimited: e.target.checked } } })} />{t('common.unlimited')}</label>
      </div>)}
    </fieldset>
    <p className="text-xs text-slate-500">{t('billingAdmin.zeroHint')}</p>
    <div className="flex flex-wrap gap-4 text-sm">
      {!isFree && <label className="flex items-center gap-2"><input type="checkbox" checked={draft.isActive} onChange={e => set({ isActive: e.target.checked })} />{t('billingAdmin.onSale')}</label>}
      <label className="flex items-center gap-2"><input type="checkbox" checked={draft.isFeatured} onChange={e => set({ isFeatured: e.target.checked })} />{t('billingAdmin.featured')}</label>
    </div>
    {isFree && <p className="text-xs text-slate-500">{t('billingAdmin.freeHint')}</p>}
    {save.isError && <Alert title={t('billingAdmin.saveFailed')}>{save.error.message}</Alert>}
    <div className="flex flex-wrap items-center gap-2">
      <Button type="submit" loading={save.isPending}>{t('questions.saveButton')}</Button>
      {onDone && <Button type="button" variant="ghost" onClick={onDone}>{t('common.cancel')}</Button>}
      {saved && <span role="status" className="text-sm text-emerald-700">{t('billingAdmin.saved')}</span>}
    </div>
  </form>
}
