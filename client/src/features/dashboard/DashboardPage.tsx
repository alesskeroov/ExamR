import { Database, Server } from 'lucide-react'
import type { ReactNode } from 'react'
import { PageHeader } from '../../components/common/PageHeader'
import { cn } from '../../lib/utils'
import { useHealth } from './api'

type Tone = 'ok' | 'error' | 'pending'

export function DashboardPage() {
  const health = useHealth()

  const apiTone: Tone = health.isPending ? 'pending' : health.isError ? 'error' : 'ok'
  const dbTone: Tone = health.isPending ? 'pending' : health.data?.databaseConnected ? 'ok' : 'error'

  return (
    <>
      <PageHeader title="Dashboard" description="Sistem vəziyyəti və tez əməliyyatlar." />

      <section aria-label="Sistem vəziyyəti" className="grid gap-4 sm:grid-cols-2">
        <StatusCard
          icon={<Server className="size-5" aria-hidden />}
          label="API"
          tone={apiTone}
          value={apiTone === 'ok' ? 'Əlçatandır' : apiTone === 'error' ? 'Əlçatan deyil' : 'Yoxlanılır…'}
        />
        <StatusCard
          icon={<Database className="size-5" aria-hidden />}
          label="Verilənlər bazası (SQL Server)"
          tone={dbTone}
          value={dbTone === 'ok' ? 'Qoşulub' : dbTone === 'error' ? 'Qoşulmayıb' : 'Yoxlanılır…'}
        />
      </section>
    </>
  )
}

const toneStyles: Record<Tone, string> = {
  ok: 'bg-emerald-500',
  error: 'bg-rose-500',
  pending: 'bg-slate-300 animate-pulse',
}

function StatusCard({ icon, label, value, tone }: { icon: ReactNode; label: string; value: string; tone: Tone }) {
  return (
    <div className="flex items-center gap-4 rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
      <div className="rounded-lg bg-slate-100 p-2.5 text-slate-600">{icon}</div>
      <div className="min-w-0">
        <p className="text-slate-500">{label}</p>
        <p className="mt-0.5 flex items-center gap-2 font-semibold">
          <span className={cn('size-2 rounded-full', toneStyles[tone])} aria-hidden />
          {value}
        </p>
      </div>
    </div>
  )
}
