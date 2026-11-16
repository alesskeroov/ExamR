import { useUsage, useUsageLimit, type UsageKind } from '../../features/usage/api'
import { Alert } from './Alert'
import { SupportContact } from './SupportContact'
import { useTranslation } from 'react-i18next'
import i18n from '../../lib/i18n'

const monthName = (index: number) => (i18n.t('common.months', { returnObjects: true }) as string[])[index]

export function UsageMeter({ kind }: { kind: UsageKind }) {
  const usage = useUsageLimit(kind)
  const { t } = useTranslation()
  const label = t(`usage.kind.${kind}`)
  if (usage.isPending) return <div role="status" className="h-12 animate-pulse rounded-xl bg-slate-100" aria-label={t('usage.loading')} />
  if (usage.error) return <Alert title={t('usage.loadFailed')}>{usage.error.message}</Alert>
  if (!usage.item || !usage.data) return null
  const { used, limit } = usage.item
  if (limit === null) return <p className="text-sm text-slate-500">{t('usage.kindUnlimited', { kind: label })}</p>
  const date = new Date(new Date(usage.data.resetsAt).getTime() + 4 * 3600000)
  const reset = t('usage.resetDate', { day: date.getUTCDate(), month: monthName(date.getUTCMonth()) })
  const ratio = limit === 0 ? 1 : Math.min(1, used / limit)
  return <div className="space-y-2 py-2">
    <p className="text-sm text-slate-600">{t('usage.count', { used, limit, kind: label, reset })}</p>
    <div role="progressbar" aria-label={t('usage.usageAria', { kind: label })} aria-valuenow={used} aria-valuemin={0} aria-valuemax={Math.max(used, limit)} className="h-1.5 overflow-hidden rounded-full bg-slate-100">
      <div className={`h-full ${ratio >= 1 ? 'bg-rose-500' : ratio >= .8 ? 'bg-amber-500' : 'bg-indigo-500'}`} style={{ width: `${ratio * 100}%` }} />
    </div>
    {usage.blocked && <Alert title={t('usage.blockedTitle')}>{t('usage.blockedText', { kind: label, reset })}<SupportContact className="mt-1" topic={t('usage.raiseTopic')} lead={t('usage.raiseLead')} /></Alert>}
  </div>
}

/** Dashboard footer: this month's limits as three thin meters in one row, one shared reset date. */
export function UsageStrip({ className }: { className?: string }) {
  const usage = useUsage()
  const { t } = useTranslation()
  const kinds: UsageKind[] = ['Upload', 'Exam', 'Battle']
  if (usage.isPending) return <div role="status" aria-label={t('usage.limitsLoading')} className={`h-12 animate-pulse rounded-lg bg-sunken ${className ?? ''}`} />
  if (usage.error || !usage.data) return null
  const date = new Date(new Date(usage.data.resetsAt).getTime() + 4 * 3600000)
  return <section aria-labelledby="usage-strip-title" className={className}>
    <div className="flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1">
      <h2 id="usage-strip-title" className="text-[15px] font-bold">{t('usage.monthTitle')}</h2>
      <p className="text-[13px] text-muted">{t('usage.resetsAt', { day: date.getUTCDate(), month: monthName(date.getUTCMonth()) })}</p>
    </div>
    <ul className="mt-4 grid gap-x-10 gap-y-5 sm:grid-cols-3">{kinds.map(kind => {
      const item = usage.data.items.find(x => x.kind === kind)
      if (!item) return null
      const ratio = item.limit === null ? 0 : item.limit === 0 ? 1 : Math.min(1, item.used / item.limit)
      return <li key={kind}>
        <div className="flex items-baseline justify-between gap-3 text-[13.5px]">
          <span className="text-muted">{t(`usage.strip.${kind}`)}</span>
          <span className="ds-num font-semibold">{item.limit === null ? t('common.unlimited') : `${item.used} / ${item.limit}`}</span>
        </div>
        {item.limit !== null && <div role="progressbar" aria-label={t('usage.limitAria', { kind: t(`usage.strip.${kind}`) })} aria-valuenow={item.used} aria-valuemin={0} aria-valuemax={Math.max(item.used, item.limit)} className="mt-2 h-1 overflow-hidden rounded-full bg-line">
          <div className={`h-full rounded-full ${ratio >= 1 ? 'bg-bad' : ratio >= .8 ? 'bg-amber-500' : 'bg-accent'}`} style={{ width: `${ratio * 100}%` }} />
        </div>}
        {item.limit !== null && item.used >= item.limit && <p className="mt-1.5 text-xs font-medium text-bad">{t('usage.limitReached')}</p>}
      </li>
    })}</ul>
  </section>
}
