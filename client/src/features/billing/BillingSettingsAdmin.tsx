import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert } from '../../components/common/Alert'
import { Button } from '../../components/common/Button'
import { useBillingSettings, useSaveBillingSettings, type BillingSettings } from './api'

export function BillingSettingsAdmin() {
  const { t } = useTranslation()
  const settings = useBillingSettings()
  if (settings.isPending) return <div role="status" className="h-40 animate-pulse rounded-xl bg-slate-100" aria-label={t('common.loading')} />
  if (settings.isError) return <Alert title={t('billingAdmin.loadFailed')}>{settings.error.message}</Alert>
  return <SettingsForm key={settings.data.updatedAt} initial={settings.data} />
}

function SettingsForm({ initial }: { initial: BillingSettings }) {
  const { t } = useTranslation()
  const save = useSaveBillingSettings()
  const [form, setForm] = useState({
    enabled: initial.enabled, whatsAppNumber: initial.whatsAppNumber ?? '', instagramUsername: initial.instagramUsername ?? '',
    paymentInstructions: initial.paymentInstructions ?? '',
  })
  const [saved, setSaved] = useState(false)
  const set = (patch: Partial<typeof form>) => { setForm({ ...form, ...patch }); setSaved(false) }
  const submit = () => save.mutate({
    enabled: form.enabled,
    whatsAppNumber: form.whatsAppNumber.replace(/[^\d]/g, '') || null,
    instagramUsername: form.instagramUsername.trim().replace(/^@/, '') || null,
    paymentInstructions: form.paymentInstructions.trim() || null,
  }, { onSuccess: () => setSaved(true) })

  return <form onSubmit={e => { e.preventDefault(); submit() }} className="grid gap-6 lg:grid-cols-2">
    <div className="space-y-4 rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
      <label className="flex items-start gap-3">
        <input type="checkbox" className="mt-1" checked={form.enabled} onChange={e => set({ enabled: e.target.checked })} />
        <span><span className="block font-medium">{t('billingAdmin.salesOpen')}</span><span className="text-sm text-slate-500">{t('billingAdmin.salesOpenHint')}</span></span>
      </label>
      <label className="block text-sm font-medium">{t('billingAdmin.whatsApp')}
        <input inputMode="numeric" maxLength={20} value={form.whatsAppNumber} placeholder="994501234567" onChange={e => set({ whatsAppNumber: e.target.value })} className="mt-1 h-11 w-full rounded-xl border border-slate-300 px-3" />
        <span className="mt-1 block text-xs font-normal text-slate-500">{t('billingAdmin.whatsAppHint')}</span>
      </label>
      <label className="block text-sm font-medium">{t('billingAdmin.instagram')}
        <input maxLength={31} value={form.instagramUsername} placeholder="examr_az" onChange={e => set({ instagramUsername: e.target.value })} className="mt-1 h-11 w-full rounded-xl border border-slate-300 px-3" />
      </label>
      <label className="block text-sm font-medium">{t('billingAdmin.instructions')}
        <textarea rows={5} maxLength={1000} value={form.paymentInstructions} placeholder={t('billingAdmin.instructionsPlaceholder')} onChange={e => set({ paymentInstructions: e.target.value })} className="mt-1 w-full rounded-xl border border-slate-300 px-3 py-2" />
        <span className="mt-1 block text-xs font-normal text-slate-500">{t('billingAdmin.instructionsHint')}</span>
      </label>
      {save.isError && <Alert title={t('billingAdmin.saveFailed')}>{save.error.message}</Alert>}
      <div className="flex items-center gap-3">
        <Button type="submit" loading={save.isPending}>{t('questions.saveButton')}</Button>
        {saved && <span role="status" className="text-sm text-emerald-700">{t('billingAdmin.saved')}</span>}
      </div>
    </div>
    <aside aria-label={t('billingAdmin.previewTitle')} className="rounded-xl border border-dashed border-slate-300 bg-slate-50 p-5 text-sm">
      <h3 className="font-semibold">{t('billingAdmin.previewTitle')}</h3>
      <p className="mt-1 text-slate-500">{t('billingAdmin.previewText')}</p>
      <div className="mt-4 rounded-xl bg-white p-4 ring-1 ring-slate-200">
        <p className="font-medium">{t('billing.step2', { amount: '7.00 AZN' })}</p>
        <p className="mt-1 whitespace-pre-line text-slate-700">{form.paymentInstructions.trim() || t('billing.noInstructions')}</p>
      </div>
    </aside>
  </form>
}
