import { Loader2, Monitor, Smartphone } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Alert } from '../../components/common/Alert'
import { Button } from '../../components/common/Button'
import { formatDate } from '../../lib/format'
import { useConfirm } from '../../lib/use-confirm'
import { useDeviceSessions, useEndOtherSessions, useEndSession } from './api'

const mobile = /Android|iPhone|iPad/

/** Signed-in browsers of this account; any other device can be signed out from here. */
export function DeviceSessions() {
  const { t } = useTranslation()
  const confirm = useConfirm()
  const { data, isPending, isError, error } = useDeviceSessions()
  const end = useEndSession()
  const endOthers = useEndOtherSessions()
  const others = data?.sessions.filter(s => !s.current).length ?? 0

  async function signOutOthers() {
    if (await confirm({ title: t('account.endOthersTitle'), description: t('account.endOthersText'), confirmLabel: t('account.endOthers'), danger: true }))
      endOthers.mutate()
  }

  return <section className="rounded-xl border border-slate-200 bg-white p-5" aria-labelledby="devices-title">
    <h2 id="devices-title" className="font-semibold">{t('account.devices')}</h2>
    {data && <p className="mt-1 text-sm text-slate-500">{t('account.devicesText', { max: data.maxActive })}</p>}
    {isPending && <div role="status" aria-label={t('common.loading')} className="mt-4 space-y-2">
      {[0, 1].map(i => <div key={i} className="h-14 animate-pulse rounded-xl bg-slate-100" />)}
    </div>}
    {isError && <div className="mt-3"><Alert title={t('account.devicesFailed')}>{error.message}</Alert></div>}
    {data && <ul className="mt-4 space-y-2">
      {data.sessions.map(session => {
        const Icon = mobile.test(session.device) ? Smartphone : Monitor
        const ending = end.isPending && end.variables === session.id
        return <li key={session.id} className="flex items-center gap-3 rounded-xl border border-slate-200 px-3.5 py-3">
          <span className="grid size-9 shrink-0 place-items-center rounded-full bg-indigo-50 text-indigo-600" aria-hidden><Icon className="size-4" /></span>
          <div className="min-w-0 flex-1">
            <p className="flex flex-wrap items-center gap-2 text-sm font-semibold text-slate-900">
              {session.device}
              {session.current && <span className="rounded-full bg-emerald-50 px-2 py-0.5 text-xs font-semibold text-emerald-700 ring-1 ring-emerald-600/20">{t('account.thisDevice')}</span>}
            </p>
            <p className="text-xs text-slate-500">
              {t('account.lastSeen', { time: formatDate(session.lastSeenAt) })} · {t('account.signedInAt', { time: formatDate(session.createdAt) })}
            </p>
          </div>
          {!session.current && <button type="button" disabled={end.isPending || endOthers.isPending} onClick={() => end.mutate(session.id)}
            className="inline-flex h-9 shrink-0 items-center gap-1.5 rounded-lg px-3 text-sm font-semibold text-rose-700 hover:bg-rose-50 disabled:cursor-not-allowed disabled:opacity-60">
            {ending && <Loader2 className="size-4 animate-spin" aria-hidden />}{t('account.endSession')}
          </button>}
        </li>
      })}
    </ul>}
    {others > 0 && <Button className="mt-4" variant="secondary" loading={endOthers.isPending} disabled={end.isPending} onClick={() => void signOutOthers()}>{t('account.endOthers')}</Button>}
    {(end.isError || endOthers.isError) && <div className="mt-3"><Alert title={t('account.endFailed')}>{(end.error ?? endOthers.error)?.message}</Alert></div>}
  </section>
}
