import { BookOpen, CheckCircle2, ChevronRight, Database, FileText, LineChart, ListChecks, Percent, Server, Swords, Timer, Upload, Users } from 'lucide-react'
import type { ReactNode } from 'react'
import { Link } from 'react-router'
import { EmptyState } from '../../components/common/EmptyState'
import { PageHeader } from '../../components/common/PageHeader'
import { formatDate } from '../../lib/format'
import { cn } from '../../lib/utils'
import { useCurrentUser } from '../auth/api'
import { useSharedDocuments } from '../documents/api'
import { sourceName } from '../exams/examSource'
import { useGroups } from '../groups/api'
import { useAllExams, useHealth } from './api'

const card = 'rounded-xl border border-slate-200 bg-white shadow-sm'
const skeleton = 'animate-pulse rounded-lg bg-slate-100'

export function DashboardPage() {
  const { data: user } = useCurrentUser()
  const exams = useAllExams()
  const groups = useGroups()
  const shared = useSharedDocuments()

  const completed = (exams.data ?? []).filter(e => e.status === 'Completed')
  const scored = completed.filter(e => e.scorePercent !== null)
  const average = scored.length ? Math.round(scored.reduce((sum, e) => sum + e.scorePercent!, 0) / scored.length) : null
  const answered = completed.reduce((sum, e) => sum + e.questionCount - (e.unanswered ?? 0), 0)
  const chart = scored.slice(0, 10).reverse()

  const stats = [
    { label: 'Tamamlanan imtahanlar', value: completed.length, icon: CheckCircle2, pending: exams.isPending },
    { label: 'Orta nəticə', value: average === null ? '—' : `${average}%`, icon: Percent, pending: exams.isPending },
    { label: 'Cavablanan suallar', value: answered, icon: ListChecks, pending: exams.isPending },
    { label: 'Qruplarım', value: groups.data?.length ?? 0, icon: Users, pending: groups.isPending },
  ]
  const actions = [
    { to: '/exams/new', label: 'İmtahana başla', hint: 'Təsadüfi suallarla vaxtlı imtahan', icon: Timer },
    { to: '/upload', label: 'Sənəd yüklə', hint: 'PDF və ya DOCX-dən suallar', icon: Upload },
    { to: '/study', label: 'Öyrənmə köməkçisi', hint: 'Sualları addım-addım məşq et', icon: BookOpen },
    { to: '/battle', label: 'Battle', hint: 'Dostlarla canlı yarış', icon: Swords },
  ]

  return <>
    <PageHeader title={user ? `Salam, ${user.nickname}` : 'Dashboard'} description="Öyrənmə fəaliyyətin, nəticələrin və yeni materialların bir yerdə." />

    <section aria-label="Statistika" className="grid grid-cols-2 gap-3 sm:gap-4 xl:grid-cols-4">
      {stats.map(s => <div key={s.label} className={cn(card, 'flex flex-col items-start gap-2 p-4 transition-shadow hover:shadow-md sm:flex-row sm:items-center sm:gap-4 sm:p-5')}>
        <span className="grid size-9 shrink-0 place-items-center rounded-xl bg-indigo-50 text-indigo-600 sm:size-11"><s.icon className="size-5" aria-hidden /></span>
        <div className="min-w-0"><p className="text-xs text-slate-500 sm:text-sm">{s.label}</p>
          {s.pending ? <div className={cn(skeleton, 'mt-1 h-7 w-16')} /> : <p className="text-2xl font-semibold tabular-nums">{s.value}</p>}</div>
      </div>)}
    </section>

    <section aria-label="Sürətli əməliyyatlar" className="mt-6 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
      {actions.map(a => <Link key={a.to} to={a.to} className={cn(card, 'group flex items-center gap-4 p-4 transition-all hover:-translate-y-0.5 hover:border-indigo-300 hover:shadow-md')}>
        <span className="grid size-10 shrink-0 place-items-center rounded-xl bg-indigo-600 text-white"><a.icon className="size-5" aria-hidden /></span>
        <span className="min-w-0 flex-1"><span className="block font-medium">{a.label}</span><span className="block truncate text-xs text-slate-500">{a.hint}</span></span>
        <ChevronRight className="size-4 text-slate-400 transition-transform group-hover:translate-x-0.5" aria-hidden />
      </Link>)}
    </section>

    <div className="mt-6 grid gap-6 xl:grid-cols-3">
      <Panel title="İmtahan nəticələri" className="xl:col-span-2">
        {exams.isPending ? <div className={cn(skeleton, 'h-56')} /> : chart.length === 0
          ? <EmptyState icon={<LineChart className="size-6" />} title="Hələ nəticə yoxdur" description="İlk imtahanını bitirdikdə nəticələr burada qrafikdə görünəcək." />
          : <ScoreChart scores={chart.map(e => ({ value: e.scorePercent!, label: sourceName(e.source), date: e.completedAt ?? e.startedAt }))} />}
      </Panel>

      <Panel title="Qruplarım" link={{ to: '/groups', label: 'Bütün qruplar' }}>
        {groups.isPending ? <Rows /> : !groups.data?.length ? <p className="text-sm text-slate-500">Hələ heç bir qrupa üzv deyilsən.</p>
          : <ul className="space-y-2">{groups.data.slice(0, 3).map(g => <li key={g.id}><Link to={`/groups/${g.id}`} className="flex items-center gap-3 rounded-lg p-2 transition-colors hover:bg-slate-50">
            <span className="grid size-9 place-items-center rounded-lg bg-indigo-50 text-indigo-600"><Users className="size-4" aria-hidden /></span>
            <span className="min-w-0 flex-1 truncate font-medium">{g.name}</span><span className="text-xs text-slate-500">{g.memberCount} üzv</span></Link></li>)}</ul>}
      </Panel>

      <Panel title="Son imtahanlar" className="xl:col-span-2" link={{ to: '/exams', label: 'Hamısına bax' }}>
        {exams.isPending ? <Rows /> : completed.length === 0 ? <p className="text-sm text-slate-500">Hələ tamamlanmış imtahan yoxdur.</p>
          : <ul className="divide-y divide-slate-100">{completed.slice(0, 5).map(e => <li key={e.id}><Link to={`/exams/${e.id}/result`} className="flex items-center gap-3 py-3 transition-colors hover:text-indigo-600">
            <span className="min-w-0 flex-1"><span className="block truncate font-medium">{sourceName(e.source)}</span><span className="text-xs text-slate-500">{formatDate(e.completedAt ?? e.startedAt)}</span></span>
            <span className="text-sm text-slate-500 tabular-nums">{e.correct ?? 0}/{e.questionCount}</span>
            <span className={cn('w-14 rounded-full px-2 py-0.5 text-center text-xs font-semibold tabular-nums', (e.scorePercent ?? 0) >= 50 ? 'bg-emerald-50 text-emerald-700' : 'bg-rose-50 text-rose-700')}>{e.scorePercent === null ? '—' : `${Math.round(e.scorePercent)}%`}</span>
          </Link></li>)}</ul>}
      </Panel>

      <Panel title="Yeni paylaşılan sənədlər">
        {shared.isPending ? <Rows /> : !shared.data?.items.length ? <p className="text-sm text-slate-500">Səninlə hələ sənəd paylaşılmayıb.</p>
          : <ul className="space-y-2">{shared.data.items.slice(0, 5).map(d => <li key={d.id}><Link to={`/study?document=${d.id}`} className="flex items-center gap-3 rounded-lg p-2 transition-colors hover:bg-slate-50">
            <FileText className="size-4 shrink-0 text-indigo-600" aria-hidden />
            <span className="min-w-0 flex-1"><span className="block truncate text-sm font-medium">{d.originalFileName}</span>
              <span className="text-xs text-slate-500">{d.sharingStatus === 'Everyone' ? 'Bütün istifadəçilər' : d.sharingStatus === 'Groups' || d.sharingStatus === 'UsersAndGroups' ? 'Qrup' : 'Şəxsi paylaşım'} · {formatDate(d.createdAt)}</span></span>
          </Link></li>)}</ul>}
      </Panel>
    </div>
  </>
}

function Panel({ title, link, className, children }: { title: string; link?: { to: string; label: string }; className?: string; children: ReactNode }) {
  return <section className={cn(card, 'p-5', className)}>
    <div className="mb-4 flex items-center justify-between gap-3"><h2 className="font-semibold">{title}</h2>
      {link && <Link to={link.to} className="text-sm font-medium text-indigo-600 hover:text-indigo-700">{link.label}</Link>}</div>
    {children}
  </section>
}

function Rows() {
  return <div className="space-y-3" role="status" aria-label="Yüklənir">{[0, 1, 2].map(i => <div key={i} className={cn(skeleton, 'h-10')} />)}</div>
}

function ScoreChart({ scores }: { scores: { value: number; label: string; date: string }[] }) {
  const w = 600, h = 220, px = 36, py = 16
  const x = (i: number) => scores.length === 1 ? w / 2 : px + (i * (w - px * 2)) / (scores.length - 1)
  const y = (v: number) => py + ((100 - v) * (h - py * 2)) / 100
  const points = scores.map((s, i) => `${x(i)},${y(s.value)}`).join(' ')
  return <svg viewBox={`0 0 ${w} ${h}`} className="h-56 w-full text-indigo-600" role="img" aria-label="Son imtahanların nəticə faizləri">
    {[0, 25, 50, 75, 100].map(v => <g key={v}><line x1={px} x2={w - px} y1={y(v)} y2={y(v)} className="stroke-slate-200" strokeDasharray="3 4" />
      <text x={px - 8} y={y(v) + 4} textAnchor="end" className="fill-slate-400 text-[11px]">{v}%</text></g>)}
    <polyline points={`${x(0)},${y(0)} ${points} ${x(scores.length - 1)},${y(0)}`} className="fill-indigo-500/10" stroke="none" />
    <polyline points={points} fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinejoin="round" strokeLinecap="round" />
    {scores.map((s, i) => <g key={i}><circle cx={x(i)} cy={y(s.value)} r="4.5" className="fill-white" stroke="currentColor" strokeWidth="2.5" />
      <text x={x(i)} y={h - 2} textAnchor="middle" className="fill-slate-400 text-[11px]">#{i + 1}</text>
      <title>{`${s.label} · ${formatDate(s.date)} · ${Math.round(s.value)}%`}</title></g>)}
  </svg>
}

type Tone = 'ok' | 'error' | 'pending'
const toneStyles: Record<Tone, string> = { ok: 'bg-emerald-500', error: 'bg-rose-500', pending: 'bg-slate-300 animate-pulse' }

/** API and SQL Server health; shown only in the Admin Panel. */
export function SystemStatus() {
  const health = useHealth()
  const apiTone: Tone = health.isPending ? 'pending' : health.isError ? 'error' : 'ok'
  const dbTone: Tone = health.isPending ? 'pending' : health.data?.databaseConnected ? 'ok' : 'error'
  return <section aria-label="Sistem vəziyyəti" className="mb-6 grid gap-4 sm:grid-cols-2">
    <StatusCard icon={<Server className="size-5" aria-hidden />} label="API" tone={apiTone} value={apiTone === 'ok' ? 'Əlçatandır' : apiTone === 'error' ? 'Əlçatan deyil' : 'Yoxlanılır…'} />
    <StatusCard icon={<Database className="size-5" aria-hidden />} label="Verilənlər bazası (SQL Server)" tone={dbTone} value={dbTone === 'ok' ? 'Qoşulub' : dbTone === 'error' ? 'Qoşulmayıb' : 'Yoxlanılır…'} />
  </section>
}

function StatusCard({ icon, label, value, tone }: { icon: ReactNode; label: string; value: string; tone: Tone }) {
  return <div className={cn(card, 'flex items-center gap-4 p-5')}>
    <div className="rounded-lg bg-slate-100 p-2.5 text-slate-600">{icon}</div>
    <div className="min-w-0"><p className="text-slate-500">{label}</p>
      <p className="mt-0.5 flex items-center gap-2 font-semibold"><span className={cn('size-2 rounded-full', toneStyles[tone])} aria-hidden />{value}</p></div>
  </div>
}
