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

export function DashboardPage() {
  const { data: user } = useCurrentUser()
  // Decode the name only right after signing in; cleared once read so in-app navigation shows it plainly.
  const [freshLogin] = useState(isFreshLogin)
  useEffect(() => { if (freshLogin) clearFreshLogin() }, [freshLogin])
  const exams = useAllExams()
  const active = useActiveExam()
  const myDocs = useMyDocuments()

  const completed = (exams.data ?? []).filter(e => e.status === 'Completed')
  const scored = completed.filter(e => e.scorePercent !== null)
  const average = scored.length ? scored.reduce((sum, e) => sum + e.scorePercent!, 0) / scored.length : null
  const answered = completed.reduce((sum, e) => sum + e.questionCount - (e.unanswered ?? 0), 0)
  const loading = exams.isPending || active.isPending

  return <div>
    <header className="mb-8 flex flex-wrap items-end justify-between gap-x-6 gap-y-4">
      <div className="min-w-0">
        <p className="ds-label">{today}</p>
        <h1 className="mt-2 truncate text-[28px] font-bold leading-tight tracking-[-0.04em] sm:text-[32px]">Salam, {user && <ScrambleText text={user.displayName || user.nickname} play={freshLogin} />}</h1>
      </div>
      <div className="flex gap-2">
        <Link to="/upload" className="inline-flex h-10 items-center rounded-[10px] border border-[var(--app-border)] px-3.5 text-sm font-semibold transition-colors hover:bg-[var(--app-sunken)]">Sənəd yüklə</Link>
        <Link to="/exams/new" className="btn-primary inline-flex h-10 items-center rounded-[10px] px-3.5 text-sm font-semibold transition-colors">İmtahana başla</Link>
      </div>
    </header>

    <div className="grid gap-10 lg:grid-cols-[minmax(0,1.2fr)_minmax(0,1fr)] lg:gap-14">

    <section className={cn('border-y border-[var(--app-border)] py-7 sm:py-8 lg:self-start', !loading && !active.data && completed.length === 0 && 'lg:col-span-2')} aria-label="Növbəti addım">
      {loading ? <Skeleton lines={3} /> : active.data
        ? <NextStep label="Yarımçıq imtahan" title="Başladığın imtahan hələ bitməyib." body="Vaxt işləməyə davam edir — qaldığın sualdan davam et.">
            <Link to={`/exams/${active.data.id}`} className="btn-primary inline-flex h-11 items-center gap-2 rounded-[10px] px-4 text-sm font-semibold">Davam et <ArrowRight className="size-4" aria-hidden /></Link>
          </NextStep>
        : completed.length
          ? <LastResult exam={completed[0]} />
          : <Onboarding hasDocument={(myDocs.data?.totalCount ?? 0) > 0} />}
    </section>

    {completed.length > 0 && <>
      <div>
      <p className="ds-muted mb-6 text-sm">
        <span className="ds-num font-semibold text-[var(--app-ink)]">{completed.length}</span> imtahan · orta <span className="ds-num font-semibold text-[var(--app-ink)]">{pct(average)}</span> · <span className="ds-num font-semibold text-[var(--app-ink)]">{answered}</span> cavablanmış sual
      </p>

      <section aria-label="Son imtahanlar">
        <div className="mb-2 flex items-baseline justify-between gap-3 border-b border-[var(--app-ink)] pb-2.5">
          <h2 className="text-[15px] font-bold">Son imtahanlar</h2>
          <Link to="/exams" className="ds-link text-[13px]">Hamısı</Link>
        </div>
        <ul className="ds-rows">{completed.slice(0, 5).map(e => <li key={e.id}>
          <Link to={`/exams/${e.id}/result`} className="group flex items-center justify-between gap-4 py-3">
            <span className="min-w-0">
              <span className="block truncate text-[14.5px] font-semibold group-hover:underline group-hover:underline-offset-4">{sourceName(e.source)}</span>
              <span className="ds-muted text-xs">{formatDate(e.completedAt ?? e.startedAt)} · <span className="ds-num">{e.correct ?? 0}/{e.questionCount}</span> düzgün</span>
            </span>
            <span className={cn('ds-num shrink-0 text-[15px] font-semibold', (e.scorePercent ?? 0) >= 70 ? 'ds-good' : (e.scorePercent ?? 0) < 50 ? 'ds-bad' : '')}>{pct(e.scorePercent)}</span>
          </Link>
        </li>)}</ul>
      </section>
      </div>
    </>}
    </div>
  </div>
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
      <Link to={exam.source.documentId && exam.source.rangeFrom !== null ? `/exams/new?retake=${exam.id}` : '/exams/new'} className="btn-primary inline-flex h-11 items-center gap-2 rounded-[10px] px-4 text-sm font-semibold">Yenidən sına <ArrowRight className="size-4" aria-hidden /></Link>
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
