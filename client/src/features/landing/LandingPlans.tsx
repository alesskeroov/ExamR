import { Check, X } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { usePublicPlans } from '../billing/api'
import { featureText, formatMoney, limitKeys, priceSuffix } from '../billing/format'

/** Package cards for signed-out visitors: Free first, then the paid packages; every card leads to sign-up. */
export function LandingPlans({ register }: { register: string }) {
  const { t } = useTranslation()
  const plans = usePublicPlans()

  if (plans.isPending) return <div role="status" aria-label={t('common.loading')} className="lp-plans">
    {[0, 1, 2].map(i => <div key={i} className="lp-mini lp-plan-skeleton" />)}
  </div>

  // Sales switched off or the list failed to load: keep the section, point to sign-up instead of prices.
  if (plans.isError || !plans.data.length) return <p className="lp-muted leading-7">
    {t('landing.plans.unavailable')} <Link viewTransition to={register} className="font-semibold underline underline-offset-2">{t('landing.register')}</Link>
  </p>

  const sorted = [...plans.data.filter(p => p.isFree), ...plans.data.filter(p => !p.isFree)]
  return <>
    <ul className="lp-plans">
      {sorted.map(plan => <li key={plan.id}>
        <article aria-labelledby={`lp-plan-${plan.id}`} className="lp-mini lp-plan" data-featured={plan.isFeatured || undefined}>
          {plan.isFeatured && <span className="lp-plan-badge">{t('billing.featured')}</span>}
          <h3 id={`lp-plan-${plan.id}`} className="lp-plan-name">{plan.name}</h3>
          {plan.description && <p className="lp-muted mt-1 text-[14px] leading-6">{plan.description}</p>}
          <p className="lp-plan-price">
            {plan.isFree
              ? <b>{t('billing.free')}</b>
              : <><b className="lp-mono">{formatMoney(plan.price)}</b><span className="lp-muted">{priceSuffix(plan)}</span></>}
          </p>
          <p className="lp-plan-limits-label">{t('billing.monthlyLimits')}</p>
          <ul className="lp-plan-features">
            {limitKeys.map(key => <li key={key} data-off={plan[key] === 0 || undefined}>
              {plan[key] === 0 ? <X className="size-4 shrink-0" aria-hidden /> : <Check className="size-4 shrink-0" strokeWidth={2.5} aria-hidden />}
              {featureText(t, key, plan[key])}
            </li>)}
          </ul>
          <Link viewTransition to={register} className={plan.isFeatured ? 'lp-btn lp-btn-primary lp-plan-cta' : 'lp-btn lp-btn-quiet lp-plan-cta'}>
            {plan.isFree ? t('landing.plans.startFree') : t('landing.plans.choose')}
          </Link>
        </article>
      </li>)}
    </ul>
    <p className="lp-muted mt-6 text-[14px] leading-6">{t('landing.plans.note')}</p>
  </>
}
