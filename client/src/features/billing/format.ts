import i18n from '../../lib/i18n'
import type { Plan, PlanLimits } from './api'

const BAKU_OFFSET_MS = 4 * 3600000

/** "16 dekabr" (Baku time). */
export function formatDay(iso: string) {
  const date = new Date(new Date(iso).getTime() + BAKU_OFFSET_MS)
  const months = i18n.t('common.months', { returnObjects: true }) as string[]
  return `${date.getUTCDate()} ${months[date.getUTCMonth()]}`
}

/** "16 dekabr 2026, 14:05" (Baku time). */
export function formatDateTime(iso: string) {
  const date = new Date(new Date(iso).getTime() + BAKU_OFFSET_MS)
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${formatDay(iso)} ${date.getUTCFullYear()}, ${pad(date.getUTCHours())}:${pad(date.getUTCMinutes())}`
}

/** True when the date is less than the given number of days away. */
export const endsWithin = (iso: string, days: number) => new Date(iso).getTime() - Date.now() < days * 86400000

/** "7 AZN", "7.50 AZN": whole amounts read without decimals. */
export const formatMoney = (amount: number) => `${Number.isInteger(amount) ? amount : amount.toFixed(2)} AZN`

/** Whole months when the package runs in 30-day steps ("3 ay"), otherwise days ("14 gün"). */
export function periodLabel(plan: Pick<Plan, 'durationDays'>, periods: number) {
  const days = plan.durationDays * periods
  return days % 30 === 0 ? i18n.t('billing.monthCount', { count: days / 30 }) : i18n.t('billing.days', { count: days })
}

/** Price suffix on a package card: "/ay", "/7 gün". */
export const priceSuffix = (plan: Pick<Plan, 'durationDays'>) =>
  plan.durationDays === 30 ? i18n.t('billing.perMonth') : `/${periodLabel(plan, 1)}`

export const limitKeys = ['monthlyUploads', 'monthlyExams', 'monthlyBattles', 'monthlyDimTests'] as const satisfies readonly (keyof PlanLimits)[]

/** Limit as shown to people: ∞ for unlimited, a dash for "not included". */
export const limitValue = (value: number | null) => value === null ? '∞' : value === 0 ? '—' : String(value)

/** Periods offered at checkout: monthly packages can be bought for several months, short ones once. */
export const periodChoices = (plan: Pick<Plan, 'durationDays'>) => plan.durationDays >= 30 ? [1, 3, 6, 12] : [1]
