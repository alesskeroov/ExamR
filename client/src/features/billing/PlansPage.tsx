import { useEffect, useRef, useState } from 'react'
import { Check, Copy, AtSign, Crown, ExternalLink, MessageCircle, X } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import type { TFunction } from 'i18next'
import { Alert } from '../../components/common/Alert'
import { Button } from '../../components/common/Button'
import { PageHeader } from '../../components/common/PageHeader'
import { useConfirm } from '../../lib/use-confirm'
import { cn } from '../../lib/utils'
import { useBilling, useBillingHistory, useCancelPaymentRequest, useCreatePaymentRequest, type Billing, type PaymentRequest, type Plan } from './api'
import { formatDateTime, formatDay, formatMoney, limitKeys, periodChoices, periodLabel, priceSuffix } from './format'

export function PlansPage() {
  const { t } = useTranslation()
  const billing = useBilling()
  const [selected, setSelected] = useState<Plan | null>(null)

  if (billing.isPending) return <div className="mx-auto max-w-6xl">
    <PageHeader title={t('billing.title')} description={t('billing.description')} />
    <div role="status" aria-label={t('common.loading')} className="grid gap-4 md:grid-cols-3">{[0, 1, 2].map(i => <div key={i} className="h-72 animate-pulse rounded-xl bg-slate-200/60" />)}</div>
  </div>
  if (billing.isError) return <div className="mx-auto max-w-6xl">
    <PageHeader title={t('billing.title')} description={t('billing.description')} />
    <Alert title={t('billing.loadFailed')}>{billing.error.message}</Alert>
  </div>

  const data = billing.data
  const current = data.plans.find(p => p.id === data.current.planId)
  const free = data.plans.find(p => p.isFree)
  const paid = data.plans.filter(p => !p.isFree)
  return <div className="mx-auto max-w-6xl">
    <PageHeader title={t('billing.title')} description={t('billing.description')} />
    <CurrentPlanCard billing={data} />
    {!data.enabled && <div className="mt-4"><Alert tone="info" title={t('billing.closedTitle')}>{t('billing.closedText')}</Alert></div>}
    {data.pending && <PendingPayment billing={data} request={data.pending} />}

    <ul className="mt-8 grid gap-5 sm:grid-cols-2 xl:grid-cols-4">
      {paid.map(plan => <li key={plan.id}>
        <PlanCard plan={plan} isCurrent={plan.id === data.current.planId} canBuy={data.enabled && !data.pending}
          selected={selected?.id === plan.id} onSelect={() => setSelected(plan)} />
      </li>)}
    </ul>
    {free && <FreeStrip plan={free} isCurrent={data.current.isFree} />}

    {selected && data.enabled && !data.pending && <Checkout key={selected.id} plan={selected} current={current} billing={data} onClose={() => setSelected(null)} />}
    <PaymentHistory />
  </div>
}

function CurrentPlanCard({ billing }: { billing: Billing }) {
  const { t } = useTranslation()
  const { current } = billing
  return <section aria-labelledby="current-plan" className="flex flex-wrap items-center gap-3 rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
    <span className={cn('flex size-10 items-center justify-center rounded-lg', current.isFree ? 'bg-slate-100 text-slate-500' : 'bg-amber-50 text-amber-600')}><Crown className="size-5" aria-hidden /></span>
    <div className="min-w-0 flex-1">
      <h2 id="current-plan" className="text-sm text-slate-500">{t('billing.currentLabel')}</h2>
      <p className="font-semibold">{current.name}{current.endsAt && <span className="font-normal text-slate-600"> · {t('billing.until', { date: formatDay(current.endsAt) })}</span>}</p>
      {current.next && <p className="text-sm text-slate-500">{t('billing.nextPlan', { name: current.next.name, date: formatDay(current.next.startsAt) })}</p>}
    </div>
  </section>
}

function PlanCard({ plan, isCurrent, canBuy, selected, onSelect }: { plan: Plan; isCurrent: boolean; canBuy: boolean; selected: boolean; onSelect: () => void }) {
  const { t } = useTranslation()
  return <article aria-labelledby={`plan-${plan.id}`} className={cn(
    'relative flex h-full flex-col overflow-hidden rounded-xl bg-white p-6 text-center shadow-sm transition-shadow hover:shadow-md',
    plan.isFeatured ? 'border-2 border-indigo-500' : 'border border-slate-200',
    selected && 'ring-2 ring-indigo-500 ring-offset-2')}>
    {plan.isFeatured && <span className="absolute right-0 top-0 rounded-bl-lg bg-indigo-600 px-2.5 py-1 text-xs font-semibold text-white">{t('billing.featured')}</span>}
    <h3 id={`plan-${plan.id}`} className="text-lg font-semibold">{plan.name}</h3>
    {plan.description && <p className="mt-1 text-sm text-slate-500">{plan.description}</p>}
    <p className="mt-4 whitespace-nowrap">
      <span className="text-4xl font-bold tracking-tight">{formatMoney(plan.price)}</span>
      <span className="text-sm font-medium text-slate-500">{priceSuffix(plan)}</span>
    </p>
    <p className="mt-5 text-left text-xs font-medium uppercase tracking-wide text-slate-500">{t('billing.monthlyLimits')}</p>
    <FeatureList plan={plan} className="mt-2 flex-1" />
    {isCurrent
      ? <Button className="mt-6 w-full" variant="secondary" disabled={!canBuy} aria-pressed={selected} onClick={onSelect}>{canBuy ? t('billing.extend') : t('billing.currentBadge')}</Button>
      : <Button className="mt-6 w-full" disabled={!canBuy} aria-pressed={selected} onClick={onSelect}>{t('billing.choose')}</Button>}
    {isCurrent && <p className="mt-2 text-xs font-medium text-emerald-700">{t('billing.currentBadge')}</p>}
  </article>
}

function FeatureList({ plan, className }: { plan: Plan; className?: string }) {
  const { t } = useTranslation()
  return <ul className={cn('space-y-2.5 text-left text-sm', className)}>
    {limitKeys.map(key => <li key={key} className="flex items-start gap-2">
      {plan[key] === 0
        ? <X className="mt-0.5 size-4 shrink-0 text-slate-400" aria-hidden />
        : <Check className="mt-0.5 size-4 shrink-0 text-indigo-600" aria-hidden />}
      <span className={plan[key] === 0 ? 'text-slate-400' : 'text-slate-700'}>{featureText(t, key, plan[key])}</span>
    </li>)}
  </ul>
}

function featureText(t: TFunction, key: (typeof limitKeys)[number], value: number | null) {
  if (value === null) return t(`billing.feature.${key}.unlimited`)
  if (value === 0) return t(`billing.feature.${key}.none`)
  return t(`billing.feature.${key}.count`, { count: value })
}

/** Free is what everyone has without a package: a compact baseline under the paid cards. */
function FreeStrip({ plan, isCurrent }: { plan: Plan; isCurrent: boolean }) {
  const { t } = useTranslation()
  return <section aria-labelledby="free-plan" className="mt-5 flex flex-col gap-3 rounded-xl border border-dashed border-slate-300 p-4 sm:flex-row sm:items-center">
    <div className="shrink-0 sm:w-40">
      <h3 id="free-plan" className="font-semibold">{plan.name} <span className="font-normal text-slate-500">· {t('billing.free')}</span></h3>
      {isCurrent && <p className="text-xs font-medium text-emerald-700">{t('billing.currentBadge')}</p>}
    </div>
    <ul className="flex flex-wrap gap-x-5 gap-y-1.5 text-sm">
      {limitKeys.map(key => <li key={key} className="flex items-center gap-1.5">
        {plan[key] === 0 ? <X className="size-4 text-slate-400" aria-hidden /> : <Check className="size-4 text-indigo-600" aria-hidden />}
        <span className={plan[key] === 0 ? 'text-slate-400' : 'text-slate-700'}>{featureText(t, key, plan[key])}</span>
      </li>)}
    </ul>
  </section>
}

function Checkout({ plan, current, billing, onClose }: { plan: Plan; current: Plan | undefined; billing: Billing; onClose: () => void }) {
  const { t } = useTranslation()
  const create = useCreatePaymentRequest()
  const [periods, setPeriods] = useState(1)
  const panel = useRef<HTMLElement>(null)
  useEffect(() => { panel.current?.scrollIntoView({ behavior: 'smooth', block: 'nearest' }) }, [])
  const choices = periodChoices(plan)
  const change = !current || current.isFree ? 'new' : current.id === plan.id ? 'extend' : plan.price < current.price ? 'downgrade' : 'upgrade'
  const channel = billing.whatsAppUrl ? 'WhatsApp' : billing.instagramUrl ? 'Instagram' : 'Other'
  return <section ref={panel} aria-labelledby="checkout-title" className="mt-6 scroll-mt-6 rounded-xl border border-indigo-200 bg-indigo-50/50 p-5">
    <div className="flex items-start justify-between gap-3">
      <h2 id="checkout-title" className="text-lg font-semibold">{t('billing.checkoutTitle', { name: plan.name })}</h2>
      <Button variant="ghost" onClick={onClose} aria-label={t('common.close')}><X className="size-4" aria-hidden /></Button>
    </div>
    {choices.length > 1 && <fieldset className="mt-4">
      <legend className="text-sm font-medium">{t('billing.periodLabel')}</legend>
      <div className="mt-2 grid grid-cols-2 gap-2 sm:grid-cols-4">{choices.map(n => <label key={n} className={cn('cursor-pointer rounded-lg border bg-white px-3 py-2 text-center text-sm transition-colors',
        periods === n ? 'border-indigo-500 ring-1 ring-indigo-500' : 'border-slate-300 hover:border-slate-400')}>
        <input type="radio" name="periods" className="sr-only" checked={periods === n} onChange={() => setPeriods(n)} />
        <span className={cn('block font-medium', periods === n && 'text-indigo-700')}>{periodLabel(plan, n)}</span>
        <span className="block text-xs text-slate-500">{formatMoney(plan.price * n)}</span>
      </label>)}</div>
    </fieldset>}
    <p className="mt-4 text-sm text-slate-700">{t(`billing.change.${change}`, { current: current?.name ?? '' })}</p>
    <p className="mt-2 text-sm">{t('billing.total')}: <strong className="text-base">{formatMoney(plan.price * periods)}</strong> · {periodLabel(plan, periods)}</p>
    {create.isError && <div className="mt-3"><Alert title={t('billing.requestFailed')}>{create.error.message}</Alert></div>}
    <Button className="mt-4" loading={create.isPending} onClick={() => create.mutate({ planId: plan.id, periods, channel }, { onSuccess: onClose })}>{t('billing.getCode')}</Button>
    <p className="mt-2 text-xs text-slate-500">{t('billing.getCodeHint')}</p>
  </section>
}

function CopyButton({ text, label }: { text: string; label: string }) {
  const { t } = useTranslation()
  const [copied, setCopied] = useState(false)
  const copy = () => void navigator.clipboard.writeText(text).then(() => { setCopied(true); setTimeout(() => setCopied(false), 2000) })
  return <Button type="button" variant="secondary" onClick={copy}>{copied ? <Check className="size-4" aria-hidden /> : <Copy className="size-4" aria-hidden />}{copied ? t('billing.copied') : label}</Button>
}

function PendingPayment({ billing, request }: { billing: Billing; request: PaymentRequest }) {
  const { t } = useTranslation()
  const cancel = useCancelPaymentRequest()
  const confirm = useConfirm()
  const ask = async () => {
    if (await confirm({ title: t('billing.cancelTitle'), description: t('billing.cancelText'), confirmLabel: t('billing.cancelRequest'), danger: true })) cancel.mutate(request.id)
  }
  return <section aria-labelledby="pending-title" className="mt-4 rounded-xl border border-amber-200 bg-amber-50/50 p-5">
    <h2 id="pending-title" className="text-lg font-semibold">{t('billing.pendingTitle')}</h2>
    <p className="mt-1 text-sm text-slate-700">{t('billing.pendingSummary', { name: request.planName, period: periodLabel({ durationDays: request.days }, 1), amount: formatMoney(request.amount) })}</p>
    <ol className="mt-4 space-y-4 text-sm">
      <li><p className="font-medium">{t('billing.step1')}</p>
        <div className="mt-2 flex flex-wrap items-center gap-3"><code className="rounded-lg bg-white px-3 py-2 font-mono text-lg font-semibold tracking-wider ring-1 ring-slate-200">{request.code}</code><CopyButton text={request.code} label={t('billing.copyCode')} /></div>
      </li>
      <li><p className="font-medium">{t('billing.step2', { amount: formatMoney(request.amount) })}</p>
        {billing.instructions ? <p className="mt-1 whitespace-pre-line text-slate-700">{billing.instructions}</p> : <p className="mt-1 text-slate-500">{t('billing.noInstructions')}</p>}
      </li>
      <li><p className="font-medium">{t('billing.step3')}</p>
        <div className="mt-2 flex flex-wrap gap-2">
          {request.whatsAppUrl && <a href={request.whatsAppUrl} target="_blank" rel="noopener noreferrer" className="btn-primary inline-flex h-10 items-center gap-2 rounded-xl px-4 text-sm font-medium"><MessageCircle className="size-4" aria-hidden />{t('billing.sendWhatsApp')}<ExternalLink className="size-3.5" aria-hidden /></a>}
          {billing.instagramUrl && <a href={billing.instagramUrl} target="_blank" rel="noopener noreferrer" onClick={() => void navigator.clipboard?.writeText(request.message).catch(() => undefined)} className="inline-flex h-10 items-center gap-2 rounded-xl border border-slate-300 bg-white px-4 text-sm font-medium text-slate-700 hover:bg-slate-50"><AtSign className="size-4" aria-hidden />{t('billing.writeInstagram')}<ExternalLink className="size-3.5" aria-hidden /></a>}
          <CopyButton text={request.message} label={t('billing.copyMessage')} />
        </div>
        <p className="mt-3 text-xs font-medium text-slate-500">{t('billing.messageLabel')}</p>
        <p className="mt-1 whitespace-pre-line rounded-lg bg-white px-3 py-2 text-slate-800 ring-1 ring-slate-200">{request.message}</p>
        {billing.instagramUrl && <p className="mt-2 text-xs text-slate-500">{t('billing.instagramHint', { account: billing.instagramUrl.split('/').pop() })}</p>}
      </li>
    </ol>
    <p className="mt-4 text-sm text-slate-600">{t('billing.pendingWait')}</p>
    {cancel.isError && <div className="mt-3"><Alert title={t('billing.cancelFailed')}>{cancel.error.message}</Alert></div>}
    <Button className="mt-3" variant="ghost" loading={cancel.isPending} onClick={() => void ask()}>{t('billing.cancelRequest')}</Button>
  </section>
}

function PaymentHistory() {
  const { t } = useTranslation()
  const history = useBillingHistory()
  const payments = history.data?.payments.filter(p => p.status !== 'Pending') ?? []
  if (!payments.length) return null
  return <section aria-labelledby="history-title" className="mt-8">
    <h2 id="history-title" className="text-base font-semibold">{t('billing.historyTitle')}</h2>
    <ul className="mt-3 divide-y divide-slate-100 rounded-xl border border-slate-200 bg-white text-sm">
      {payments.map(p => <li key={p.id} className="flex flex-wrap items-center justify-between gap-2 px-4 py-3">
        <span><span className="font-medium">{p.planName}</span> · {formatMoney(p.amount)} · <span className="font-mono text-xs">{p.code}</span></span>
        <span className="text-slate-500">{formatDateTime(p.createdAt)} · <StatusLabel status={p.status} />{p.adminNote && <> · {p.adminNote}</>}</span>
      </li>)}
    </ul>
  </section>
}

function StatusLabel({ status }: { status: PaymentRequest['status'] }) {
  const { t } = useTranslation()
  const tone = status === 'Approved' ? 'text-emerald-700' : status === 'Rejected' ? 'text-rose-700' : status === 'Pending' ? 'text-amber-700' : 'text-slate-500'
  return <span className={cn('font-medium', tone)}>{t(`billing.status.${status}`)}</span>
}
