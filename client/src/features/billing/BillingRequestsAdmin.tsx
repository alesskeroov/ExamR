import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert } from '../../components/common/Alert'
import { Button } from '../../components/common/Button'
import { EmptyState } from '../../components/common/EmptyState'
import { cn } from '../../lib/utils'
import { AdminDialog } from './AdminDialog'
import { useAdminPayments, useApprovePayment, usePaymentPreview, useRejectPayment, type AdminPayment, type PaymentStatus } from './api'
import { formatDateTime, formatDay, formatMoney, periodLabel } from './format'

const PAGE_SIZE = 25
const filters: (PaymentStatus | 'All')[] = ['Pending', 'Approved', 'Rejected', 'Cancelled', 'All']

export function BillingRequestsAdmin() {
  const { t } = useTranslation()
  const [status, setStatus] = useState<PaymentStatus | 'All'>('Pending')
  const [term, setTerm] = useState('')
  const [query, setQuery] = useState('')
  const [page, setPage] = useState(1)
  const [approving, setApproving] = useState<AdminPayment | null>(null)
  const [rejecting, setRejecting] = useState<AdminPayment | null>(null)
  const payments = useAdminPayments(status, query, page)
  useEffect(() => { const timer = setTimeout(() => { setQuery(term.trim()); setPage(1) }, 250); return () => clearTimeout(timer) }, [term])
  const rows = payments.data?.items ?? []

  return <section className="space-y-4">
    <div className="flex flex-wrap items-end gap-3">
      <div role="tablist" aria-label={t('billingAdmin.requestFilterAria')} className="inline-flex flex-wrap rounded-lg border border-slate-200 bg-white p-1">
        {filters.map(f => <button key={f} type="button" role="tab" aria-selected={status === f} onClick={() => { setStatus(f); setPage(1) }}
          className={cn('rounded-md px-3 py-1.5 text-sm font-medium', status === f ? 'bg-indigo-600 text-white' : 'text-slate-600 hover:bg-slate-100')}>
          {f === 'All' ? t('billingAdmin.status.all') : t(`billing.status.${f}`)}
        </button>)}
      </div>
      <label className="block min-w-60 flex-1 text-sm font-medium">{t('billingAdmin.search')}
        <input type="search" maxLength={256} value={term} onChange={e => setTerm(e.target.value)} placeholder={t('billingAdmin.requestSearchPlaceholder')} className="mt-1 h-11 w-full rounded-xl border border-slate-300 px-3" />
      </label>
    </div>

    {payments.isPending && <div role="status" className="h-40 animate-pulse rounded-xl bg-slate-100" aria-label={t('common.loading')} />}
    {payments.isError && <Alert title={t('billingAdmin.loadFailed')}>{payments.error.message}</Alert>}
    {payments.data && rows.length === 0 && <EmptyState title={status === 'Pending' ? t('billingAdmin.noPending') : t('billingAdmin.noRequests')} />}
    {rows.length > 0 && <div className="relative overflow-x-auto rounded-xl border border-slate-200 bg-white">
      <table className="w-full min-w-[820px] text-left text-sm">
        <thead className="bg-slate-50 text-slate-600"><tr>
          <th className="p-3">{t('billingAdmin.code')}</th><th className="p-3">{t('common.user')}</th><th className="p-3">{t('billingAdmin.plan')}</th>
          <th className="p-3">{t('billingAdmin.amount')}</th><th className="p-3">{t('billingAdmin.created')}</th><th className="p-3">{t('billingAdmin.state')}</th>
        </tr></thead>
        <tbody>{rows.map(row => <tr key={row.request.id} className="border-t border-slate-100 align-top">
          <td className="whitespace-nowrap p-3 font-mono font-semibold">{row.request.code}</td>
          <td className="p-3"><p className="font-medium">{row.nickname}</p><p className="text-xs text-slate-500">{row.email}</p></td>
          <td className="p-3">{row.request.planName}<p className="text-xs text-slate-500">{periodLabel({ durationDays: row.request.days }, 1)} · {t(`billingAdmin.channel.${row.request.channel}`)}</p></td>
          <td className="whitespace-nowrap p-3">{formatMoney(row.request.amount)}</td>
          <td className="whitespace-nowrap p-3">{formatDateTime(row.request.createdAt)}</td>
          <td className="p-3">{row.request.status === 'Pending'
            ? <div className="flex flex-wrap gap-2"><Button onClick={() => setApproving(row)}>{t('billingAdmin.approve')}</Button><Button variant="secondary" onClick={() => setRejecting(row)}>{t('billingAdmin.reject')}</Button></div>
            : <><p className="font-medium">{t(`billing.status.${row.request.status}`)}</p>
              <p className="text-xs text-slate-500">{row.request.reviewedAt && formatDateTime(row.request.reviewedAt)}{row.reviewedBy && ` · ${row.reviewedBy}`}</p>
              {row.request.adminNote && <p className="text-xs text-slate-500">{row.request.adminNote}</p>}</>}
          </td>
        </tr>)}</tbody>
      </table>
    </div>}
    {payments.data && payments.data.totalCount > PAGE_SIZE && <div className="flex items-center gap-3">
      <Button variant="secondary" disabled={page === 1} onClick={() => setPage(p => p - 1)}>{t('common.previous')}</Button>
      <span className="text-sm">{page} / {Math.ceil(payments.data.totalCount / PAGE_SIZE)}</span>
      <Button variant="secondary" disabled={page * PAGE_SIZE >= payments.data.totalCount} onClick={() => setPage(p => p + 1)}>{t('common.next')}</Button>
    </div>}
    {approving && <ApproveDialog row={approving} onClose={() => setApproving(null)} />}
    {rejecting && <RejectDialog row={rejecting} onClose={() => setRejecting(null)} />}
  </section>
}

function ApproveDialog({ row, onClose }: { row: AdminPayment; onClose: () => void }) {
  const { t } = useTranslation()
  const [periods, setPeriods] = useState(row.request.periods)
  const [bonus, setBonus] = useState('')
  const [note, setNote] = useState('')
  const bonusDays = bonus === '' ? null : Math.min(365, Math.max(0, Number(bonus) || 0))
  const preview = usePaymentPreview(row.request.id, periods, bonusDays)
  const approve = useApprovePayment()
  const monthly = row.request.days / row.request.periods >= 30
  const p = preview.data
  return <AdminDialog title={t('billingAdmin.approveTitle', { code: row.request.code })} busy={approve.isPending} onClose={onClose}>
    <form className="space-y-4" onSubmit={e => { e.preventDefault(); approve.mutate({ id: row.request.id, periods, bonusDays, note: note.trim() || undefined }, { onSuccess: onClose }) }}>
      <p className="text-sm">{t('billingAdmin.approveWho', { name: row.nickname, plan: row.request.planName, amount: formatMoney(row.request.amount) })}</p>
      <div className="grid grid-cols-2 gap-3">
        {monthly && <label className="block text-sm font-medium">{t('billingAdmin.periods')}
          <input type="number" min={1} max={12} value={periods} onChange={e => setPeriods(Math.min(12, Math.max(1, Number(e.target.value) || 1)))} className="mt-1 h-11 w-full rounded-xl border border-slate-300 px-3" />
          <span className="mt-1 block text-xs font-normal text-slate-500">= {periodLabel({ durationDays: row.request.days / row.request.periods }, periods)}</span>
        </label>}
        <label className="block text-sm font-medium">{t('billingAdmin.bonusDays')}
          <input type="number" min={0} max={365} value={bonus} placeholder={p ? String(p.bonusDays) : ''} onChange={e => setBonus(e.target.value)} className="mt-1 h-11 w-full rounded-xl border border-slate-300 px-3" />
        </label>
      </div>
      {preview.isError && <Alert title={t('billingAdmin.previewFailed')}>{preview.error.message}</Alert>}
      {p && <div role="status" className="rounded-xl bg-slate-50 p-3 text-sm">
        <p className="font-medium">{t(`billingAdmin.kind.${p.kind}`, { current: p.currentPlanName ?? '', plan: row.request.planName })}</p>
        <p className="mt-1 text-slate-600">{t('billingAdmin.previewPeriod', { from: formatDay(p.startsAt), to: formatDay(p.endsAt) })}{p.bonusDays > 0 && ` · ${t('billingAdmin.previewBonus', { count: p.bonusDays })}`}</p>
      </div>}
      <label className="block text-sm font-medium">{t('billingAdmin.note')}
        <input maxLength={500} value={note} onChange={e => setNote(e.target.value)} className="mt-1 h-11 w-full rounded-xl border border-slate-300 px-3" />
      </label>
      {approve.isError && <Alert title={t('billingAdmin.approveFailed')}>{approve.error.message}</Alert>}
      <div className="flex flex-wrap gap-2">
        <Button type="submit" loading={approve.isPending} disabled={!p}>{t('billingAdmin.approveConfirm')}</Button>
        <Button type="button" variant="ghost" disabled={approve.isPending} onClick={onClose}>{t('common.cancel')}</Button>
      </div>
    </form>
  </AdminDialog>
}

function RejectDialog({ row, onClose }: { row: AdminPayment; onClose: () => void }) {
  const { t } = useTranslation()
  const [note, setNote] = useState('')
  const reject = useRejectPayment()
  return <AdminDialog title={t('billingAdmin.rejectTitle', { code: row.request.code })} busy={reject.isPending} onClose={onClose}>
    <form className="space-y-4" onSubmit={e => { e.preventDefault(); reject.mutate({ id: row.request.id, note: note.trim() || undefined }, { onSuccess: onClose }) }}>
      <label className="block text-sm font-medium">{t('billingAdmin.rejectReason')}
        <input maxLength={500} value={note} placeholder={t('billingAdmin.rejectPlaceholder')} onChange={e => setNote(e.target.value)} className="mt-1 h-11 w-full rounded-xl border border-slate-300 px-3" />
      </label>
      <p className="text-xs text-slate-500">{t('billingAdmin.rejectHint')}</p>
      {reject.isError && <Alert title={t('billingAdmin.rejectFailed')}>{reject.error.message}</Alert>}
      <div className="flex flex-wrap gap-2">
        <Button type="submit" loading={reject.isPending}>{t('billingAdmin.reject')}</Button>
        <Button type="button" variant="ghost" disabled={reject.isPending} onClick={onClose}>{t('common.cancel')}</Button>
      </div>
    </form>
  </AdminDialog>
}
