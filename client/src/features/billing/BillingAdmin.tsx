import { useSearchParams } from 'react-router'
import { useTranslation } from 'react-i18next'
import { cn } from '../../lib/utils'
import { usePendingPaymentCount } from './api'
import { BillingPlansAdmin } from './BillingPlansAdmin'
import { BillingRequestsAdmin } from './BillingRequestsAdmin'
import { BillingSettingsAdmin } from './BillingSettingsAdmin'
import { BillingUsersAdmin } from './BillingUsersAdmin'

const tabs = ['users', 'requests', 'plans', 'settings'] as const
type Tab = (typeof tabs)[number]

/** Admin → "Packages and limits": users (single + bulk), payment requests, packages, settings. */
export function BillingAdmin() {
  const { t } = useTranslation()
  const [params, setParams] = useSearchParams()
  const pending = usePendingPaymentCount()
  const tab: Tab = tabs.find(x => x === params.get('tab')) ?? 'users'
  const open = (next: Tab) => setParams(prev => { const p = new URLSearchParams(prev); p.set('tab', next); return p })
  return <section className="space-y-5">
    <div role="tablist" aria-label={t('billingAdmin.tabsAria')} className="flex flex-wrap gap-1 border-b border-slate-200">
      {tabs.map(id => <button key={id} type="button" role="tab" aria-selected={tab === id} onClick={() => open(id)}
        className={cn('-mb-px flex items-center gap-2 border-b-2 px-4 py-2 text-sm font-medium', tab === id ? 'border-indigo-600 text-indigo-700' : 'border-transparent text-slate-600 hover:text-slate-900')}>
        {t(`billingAdmin.tabs.${id}`)}
        {id === 'requests' && pending > 0 && <span className="rounded-full bg-amber-500 px-1.5 text-xs font-semibold text-white" aria-label={t('billingAdmin.pendingCount', { count: pending })}>{pending}</span>}
      </button>)}
    </div>
    <div role="tabpanel">
      {tab === 'users' ? <BillingUsersAdmin /> : tab === 'requests' ? <BillingRequestsAdmin /> : tab === 'plans' ? <BillingPlansAdmin /> : <BillingSettingsAdmin />}
    </div>
  </section>
}
