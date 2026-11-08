import { UsageStrip } from '../../components/common/UsageMeter'
import { ArrowRight, Check, Database, Server } from 'lucide-react'
import { useEffect, useState, type ReactNode } from 'react'
import { Link } from 'react-router'
import { ScrambleText } from '../../components/common/ScrambleText'
import { clearFreshLogin, isFreshLogin } from '../../lib/fresh-login'
import { formatDate } from '../../lib/format'
import { cn } from '../../lib/utils'
import { useCurrentUser } from '../auth/api'
import { useMyDocuments } from '../documents/api'
import { useActiveExam, type ExamSummary } from '../exams/api'
import { sourceName } from '../exams/examSource'
import { useAllExams, useHealth } from './api'

// Built by hand: many browsers ship without Azerbaijani date names and fall back to "M11 2, Mon".
const days = ['Bazar', 'Bazar ertəsi', 'Çərşənbə axşamı', 'Çərşənbə', 'Cümə axşamı', 'Cümə', 'Şənbə']
const months = ['yanvar', 'fevral', 'mart', 'aprel', 'may', 'iyun', 'iyul', 'avqust', 'sentyabr', 'oktyabr', 'noyabr', 'dekabr']
const now = new Date()
const today = `${days[now.getDay()]}, ${now.getDate()} ${months[now.getMonth()]}`
const pct = (value: number | null) => (value === null ? '—' : `${Math.round(value)}%`)
/** 70%+ reads as good, under 50% as needing work. */
const scoreTone = (value: number | null) => (value === null ? '' : value >= 70 ? 'ds-good' : value < 50 ? 'ds-bad' : '')
/** Setup link that repeats an exam's PDF and range when it had one. */
const retakeLink = (exam: ExamSummary) => (exam.source.documentId && exam.source.rangeFrom !== null ? `/exams/new?retake=${exam.id}` : '/exams/new')

export function DashboardPage() {
  const { data: user } = useCurrentUser()
  // Decode the name only right after signing in; cleared once read so in-app navigation shows it plainly.
  const [freshLogin] = useState(isFreshLogin)
  useEffect(() => { if (freshLogin) clearFreshLogin() }, [freshLogin])
  const exams = useAllExams()
  const active = useActiveExam()
  const myDocs = useMyDocuments()

  // History arrives newest first.
  const completed = (exams.data ?? []).filter(e => e.status === 'Completed')
  const loading = exams.isPending || active.isPending
  const firstRun = !loading && !active.data && completed.length === 0

  return <div>
    <header className="mb-8 flex flex-wrap items-end justify-between gap-x-6 gap-y-4">
      <div className="min-w-0">
        <p className="ds-label">{today}</p>
        <h1 className="mt-1.5 truncate text-[28px] font-bold leading-tight tracking-[-0.04em] sm:text-[34px]">Salam, {user && <ScrambleText text={user.displayName || user.nickname} play={freshLogin} />}</h1>
      </div>
      <div className="flex gap-2">
        <Link to="/upload" className="inline-flex h-10 items-center rounded-[10px] border border-line bg-surface px-3.5 text-sm font-semibold transition-colors hover:bg-sunken">Sənəd yüklə</Link>
        <Link to="/exams/new" className="btn-primary inline-flex h-10 items-center rounded-[10px] px-3.5 text-sm font-semibold transition-colors">İmtahana başla</Link>
      </div>
    </header>

    {/* The one raised surface on the page: what to do next. Everything else is ruled rows. */}
    <div className={cn('grid gap-8 lg:gap-12', !firstRun && 'lg:grid-cols-[minmax(0,1.15fr)_minmax(0,1fr)]')}>
      <section aria-label="Növbəti addım" className="rounded-2xl border border-line bg-surface p-6 sm:p-8 lg:self-start">
        {loading ? <Skeleton lines={3} /> : active.data
          ? <NextStep label="Yarımçıq imtahan" title="Başladığın imtahan hələ bitməyib." body="Vaxt işləməyə davam edir. Qaldığın sualdan davam et.">
              <Link to={`/exams/${active.data.id}`} className="btn-primary inline-flex h-11 items-center rounded-[10px] px-4 text-sm font-semibold">Davam et</Link>
            </NextStep>
          : completed.length
            ? <LastResult exam={completed[0]} />
            : <Onboarding hasDocument={(myDocs.data?.totalCount ?? 0) > 0} />}
      </section>
      {!firstRun && <Scorecard exams={completed} loading={loading} />}
    </div>

    {completed.length > 0 && <div className="mt-12 grid gap-12 lg:grid-cols-2">
      <RecentExams exams={completed} />
      <SourceScores exams={completed} />
    </div>}

    <UsageStrip className="mt-12 border-t border-line pt-6" />
  </div>
}

function SectionHead({ id, title, link }: { id: string; title: string; link?: ReactNode }) {
  return <div className="mb-1 flex items-baseline justify-between gap-3 border-b border-ink pb-2.5">
    <h2 id={id} className="text-[15px] font-bold">{title}</h2>{link}
  </div>
}

/** Totals plus the score line of the last exams, oldest to newest, against the 70% mark. */
function Scorecard({ exams, loading }: { exams: ExamSummary[]; loading: boolean }) {
  const scored = exams.filter(e => e.scorePercent !== null)
  const average = scored.length ? scored.reduce((sum, e) => sum + e.scorePercent!, 0) / scored.length : null
  const answered = exams.reduce((sum, e) => sum + e.questionCount - (e.unanswered ?? 0), 0)
  const trend = scored.slice(0, 12).reverse().map(e => e.scorePercent!)
  const stats: [string, string][] = [['İmtahan', String(exams.length)], ['Orta bal', pct(average)], ['Cavablanmış sual', String(answered)]]
  return <section aria-labelledby="dash-score">
    <SectionHead id="dash-score" title="Göstəricilər" />
    {loading ? <Skeleton lines={3} /> : <>
      <dl className="grid grid-cols-3 divide-x divide-line border-b border-line">
        {stats.map(([k, v]) => <div key={k} className="min-w-0 px-3 py-4 first:pl-0">
          <dt className="min-h-[2lh] text-[12.5px] leading-snug text-muted sm:min-h-0">{k}</dt>
          <dd className="ds-num mt-1 text-[24px] font-bold leading-none sm:text-[28px]">{v}</dd>
        </div>)}
      </dl>
      <div className="pt-5">
        <div className="flex items-baseline justify-between gap-3 text-[13px]">
          <span className="font-semibold">Bal dinamikası</span>
          {trend.length >= 2 && <span className="text-muted">son <span className="ds-num">{trend.length}</span> imtahan</span>}
        </div>
        {trend.length >= 2 ? <ScoreLine values={trend} /> : <p className="mt-2 text-[13.5px] text-muted">Xətt ikinci imtahandan sonra görünəcək.</p>}
      </div>
    </>}
  </section>
}

function ScoreLine({ values }: { values: number[] }) {
  const x = (i: number) => (i / (values.length - 1)) * 100
  const last = values[values.length - 1]
  return <div className="relative mt-3 h-28" role="img" aria-label={`Son ${values.length} imtahanın balı: ${values.map(v => `${Math.round(v)}%`).join(', ')}`}>
    <svg viewBox="0 0 100 100" preserveAspectRatio="none" className="absolute inset-0 size-full overflow-visible" aria-hidden>
      <line x1="0" x2="100" y1="30" y2="30" className="stroke-line" strokeWidth="1" strokeDasharray="3 3" vectorEffect="non-scaling-stroke" />
      <polyline points={values.map((v, i) => `${x(i)},${100 - v}`).join(' ')} fill="none" className="stroke-accent" strokeWidth="2" strokeLinejoin="round" strokeLinecap="round" vectorEffect="non-scaling-stroke" />
    </svg>
    <span className="absolute left-0 top-[30%] -translate-y-full pb-0.5 text-[11px] text-muted" aria-hidden>70%</span>
    <span className="absolute size-2.5 -translate-x-1/2 -translate-y-1/2 rounded-full border-2 border-paper bg-accent" style={{ left: '100%', top: `${100 - last}%` }} aria-hidden />
  </div>
}

function RecentExams({ exams }: { exams: ExamSummary[] }) {
  return <section aria-labelledby="dash-recent">
    <SectionHead id="dash-recent" title="Son imtahanlar" link={<Link to="/exams" className="ds-link text-[13px]">Hamısı</Link>} />
    <ul className="ds-rows">{exams.slice(0, 5).map(e => <li key={e.id}>
      <Link to={`/exams/${e.id}/result`} className="group flex items-center justify-between gap-4 py-3">
        <span className="min-w-0">
          <span className="block truncate text-[14.5px] font-semibold group-hover:underline group-hover:underline-offset-4">{sourceName(e.source)}</span>
          <span className="text-xs text-muted">{formatDate(e.completedAt ?? e.startedAt)}, <span className="ds-num">{e.correct ?? 0}/{e.questionCount}</span> düzgün</span>
        </span>
        <span className={cn('ds-num shrink-0 text-[15px] font-semibold', scoreTone(e.scorePercent))}>{pct(e.scorePercent)}</span>
      </Link>
    </li>)}</ul>
  </section>
}

/** Average score per PDF, weakest first, so the next thing to repeat is at the top. */
function SourceScores({ exams }: { exams: ExamSummary[] }) {
  const groups = new Map<string, { name: string; scores: number[]; latest: ExamSummary }>()
  for (const e of exams) {
    if (e.scorePercent === null) continue
    const key = e.source.documentId ?? `name:${sourceName(e.source)}`
    const group = groups.get(key) ?? { name: sourceName(e.source), scores: [], latest: e }
    group.scores.push(e.scorePercent)
    groups.set(key, group)
  }
  const rows = [...groups.values()].map(g => ({ ...g, average: g.scores.reduce((a, b) => a + b, 0) / g.scores.length }))
    .sort((a, b) => a.average - b.average).slice(0, 5)
  if (!rows.length) return null
  return <section aria-labelledby="dash-sources">
    <SectionHead id="dash-sources" title="Sənədlər üzrə orta bal" />
    <ul className="ds-rows">{rows.map(r => <li key={r.latest.id} className="grid grid-cols-[minmax(0,1fr)_auto] items-center gap-x-4 gap-y-2 py-3">
      <span className="min-w-0">
        <span className="block truncate text-[14.5px] font-semibold">{r.name}</span>
        <span className="text-xs text-muted"><span className="ds-num">{r.scores.length}</span> imtahan</span>
      </span>
      <span className="flex items-center gap-4">
        <span className={cn('ds-num w-11 text-right text-[15px] font-semibold', scoreTone(r.average))}>{pct(r.average)}</span>
        <Link to={retakeLink(r.latest)} className="ds-link text-[13px]" aria-label={`${r.name}: yenidən sına`}>Təkrar et</Link>
      </span>
      <div className="col-span-2 h-1 overflow-hidden rounded-full bg-line" aria-hidden>
        <div className={cn('h-full rounded-full', r.average < 50 ? 'bg-bad' : 'bg-ink')} style={{ width: `${Math.max(2, r.average)}%` }} />
      </div>
    </li>)}</ul>
  </section>
}

function NextStep({ label, title, body, children }: { label: string; title: string; body: string; children: ReactNode }) {
  return <div>
    <p className="ds-label">{label}</p>
    <h2 className="mt-3 max-w-[22ch] text-[24px] font-bold leading-[1.15] tracking-[-0.035em] sm:text-[30px]">{title}</h2>
    <p className="ds-muted mt-3 max-w-[48ch] text-[15px] leading-7">{body}</p>
    <div className="mt-6 flex flex-wrap items-center gap-x-5 gap-y-3">{children}</div>
  </div>
}

function LastResult({ exam }: { exam: ExamSummary }) {
  const score = exam.scorePercent
  const verdict = score === null ? 'Nəticə hesablanmadı.' : score >= 85 ? 'Çox yaxşı nəticə.' : score >= 70 ? 'Yaxşı nəticə — bir az daha.' : score >= 50 ? 'Ortadır. Səhvlərə bax.' : 'Bu mövzunu təkrar etmək lazımdır.'
  return <div>
    <p className="ds-label">Son imtahan · {formatDate(exam.completedAt ?? exam.startedAt)}</p>
    <div className="mt-3 flex flex-wrap items-end gap-x-6 gap-y-2">
      <span className={cn('ds-num text-[56px] font-bold leading-none sm:text-[72px]', score !== null && score < 50 && 'ds-bad')}>{pct(score)}</span>
      <span className="mb-1.5 min-w-0">
        <span className="block max-w-[28ch] truncate text-[17px] font-semibold">{sourceName(exam.source)}</span>
        <span className="ds-muted text-sm"><span className="ds-num">{exam.correct ?? 0}</span> düzgün · <span className="ds-num">{exam.incorrect ?? 0}</span> səhv · <span className="ds-num">{exam.unanswered ?? 0}</span> boş</span>
      </span>
    </div>
    <p className="mt-4 text-[15px] font-medium"><span className="ds-mark">{verdict}</span></p>
    <div className="mt-6 flex flex-wrap items-center gap-x-5 gap-y-3">
      <Link to={retakeLink(exam)} className="btn-primary inline-flex h-11 items-center rounded-[10px] px-4 text-sm font-semibold">Yenidən sına</Link>
      <Link to={`/exams/${exam.id}/result`} className="ds-link text-sm">Cavablara bax</Link>
    </div>
  </div>
}

function Onboarding({ hasDocument }: { hasDocument: boolean }) {
  const steps = [
    { done: hasDocument, title: 'Sənəd yüklə', text: 'Sual-cavab olan PDF və ya DOCX faylı.', to: '/upload' },
    { done: false, title: 'İlk imtahanı ver', text: 'Sual aralığını və sayını seç, vaxt başlasın.', to: '/exams/new' },
    { done: false, title: 'Nəticəni təhlil et', text: 'Səhvlərinə nəticə səhifəsində bax.', to: '/exams' },
  ]
  const next = steps.findIndex(s => !s.done)
  return <div>
    <p className="ds-label">Başlanğıc</p>
    <h2 className="mt-3 text-[24px] font-bold leading-[1.15] tracking-[-0.035em] sm:text-[30px]">Üç addımda ilk nəticən.</h2>
    <ol className="mt-6 grid gap-3 lg:grid-cols-3">{steps.map((s, i) => <li key={s.title}>
      <Link to={s.to} className={cn('group flex items-start gap-4 rounded-xl px-3 py-3 transition-colors hover:bg-[var(--app-sunken)]', i === next && 'ds-sunken')}>
        <span className={cn('ds-num mt-0.5 grid size-7 shrink-0 place-items-center rounded-full border text-xs font-semibold', s.done ? 'border-transparent bg-[var(--app-ink)] text-[var(--app-on-ink)]' : 'border-[var(--app-border)]')}>
          {s.done ? <Check className="size-3.5" strokeWidth={3} aria-label="Edilib" /> : i + 1}
        </span>
        <span className="min-w-0 flex-1"><span className={cn('block font-semibold', s.done && 'ds-muted line-through')}>{s.title}</span><span className="ds-muted text-sm">{s.text}</span></span>
        {i === next && <ArrowRight className="mt-1 size-4 shrink-0 transition-transform group-hover:translate-x-0.5" aria-hidden />}
      </Link>
    </li>)}</ol>
  </div>
}

function Skeleton({ lines }: { lines: number }) {
  return <div className="grid gap-3 py-2" role="status" aria-label="Yüklənir">{Array.from({ length: lines }, (_, i) => <div key={i} className="h-5 animate-pulse rounded bg-[var(--app-sunken)]" style={{ width: `${90 - i * 15}%` }} />)}</div>
}

type Tone = 'ok' | 'error' | 'pending'
const toneStyles: Record<Tone, string> = { ok: 'bg-emerald-500', error: 'bg-rose-500', pending: 'bg-slate-300 animate-pulse' }

/** API and SQL Server health; shown only in the Admin Panel. */
export function SystemStatus() {
  const health = useHealth()
  const apiTone: Tone = health.isPending ? 'pending' : health.isError ? 'error' : 'ok'
  const dbTone: Tone = health.isPending ? 'pending' : health.data?.databaseConnected ? 'ok' : 'error'
  return <section aria-label="Sistem vəziyyəti" className="mb-6 flex flex-wrap gap-x-8 gap-y-3 border-y border-[var(--app-border)] py-3 text-sm">
    <StatusItem icon={<Server className="size-4" aria-hidden />} label="API" tone={apiTone} value={apiTone === 'ok' ? 'Əlçatandır' : apiTone === 'error' ? 'Əlçatan deyil' : 'Yoxlanılır…'} />
    <StatusItem icon={<Database className="size-4" aria-hidden />} label="SQL Server" tone={dbTone} value={dbTone === 'ok' ? 'Qoşulub' : dbTone === 'error' ? 'Qoşulmayıb' : 'Yoxlanılır…'} />
  </section>
}

function StatusItem({ icon, label, value, tone }: { icon: ReactNode; label: string; value: string; tone: Tone }) {
  return <span className="flex items-center gap-2"><span className="ds-muted">{icon}</span><span className="ds-muted">{label}</span>
    <span className="flex items-center gap-1.5 font-semibold"><span className={cn('size-2 rounded-full', toneStyles[tone])} aria-hidden />{value}</span></span>
}
