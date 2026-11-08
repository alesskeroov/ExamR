import { useUsage, useUsageLimit, type UsageKind } from '../../features/usage/api'
import { Alert } from './Alert'

const labels: Record<UsageKind, string> = { Upload: 'sənəd', Exam: 'imtahan', Battle: 'battle', AiRequest: 'AI sorğusu' }
const months = ['yanvar', 'fevral', 'mart', 'aprel', 'may', 'iyun', 'iyul', 'avqust', 'sentyabr', 'oktyabr', 'noyabr', 'dekabr']

export function UsageMeter({ kind }: { kind: UsageKind }) {
  const usage = useUsageLimit(kind)
  if (usage.isPending) return <div role="status" className="h-12 animate-pulse rounded-xl bg-slate-100" aria-label="Limit yüklənir" />
  if (usage.error) return <Alert title="Limit yüklənmədi">{usage.error.message}</Alert>
  if (!usage.item || !usage.data) return null
  const { used, limit } = usage.item
  if (limit === null) return <p className="text-sm text-slate-500">{labels[kind]} · Limitsiz</p>
  const date = new Date(new Date(usage.data.resetsAt).getTime() + 4 * 3600000)
  const reset = `${date.getUTCDate()} ${months[date.getUTCMonth()]}, 00:00`
  const ratio = limit === 0 ? 1 : Math.min(1, used / limit)
  return <div className="space-y-2 py-2">
    <p className="text-sm text-slate-600">{used} / {limit} {labels[kind]} · {reset}-da yenilənir</p>
    <div role="progressbar" aria-label={`${labels[kind]} istifadəsi`} aria-valuenow={used} aria-valuemin={0} aria-valuemax={Math.max(used, limit)} className="h-1.5 overflow-hidden rounded-full bg-slate-100">
      <div className={`h-full ${ratio >= 1 ? 'bg-rose-500' : ratio >= .8 ? 'bg-amber-500' : 'bg-indigo-500'}`} style={{ width: `${ratio * 100}%` }} />
    </div>
    {usage.blocked && <Alert title="Aylıq limit bitib">Yeni {labels[kind]} üçün limit {reset}-da (Bakı vaxtı) yenilənir.</Alert>}
  </div>
}

export function UsageCard() {
  const usage = useUsage()
  return <section aria-label="Bu ayın limitləri" className="my-6 rounded-xl border border-slate-200 bg-white p-5">
    <h2 className="mb-3 font-semibold">Bu ayın limitləri</h2>
    <div className="grid gap-4 sm:grid-cols-2">{(usage.data?.items.map(x => x.kind) ?? ['Upload', 'Exam', 'Battle', 'AiRequest'] as UsageKind[]).map(kind => <UsageMeter key={kind} kind={kind} />)}</div>
  </section>
}
