import { useEffect, useId, useRef, useState } from 'react'
import { Check, FileText, Loader2, Pencil, RotateCcw, Sparkles, X } from 'lucide-react'
import { useQueryClient } from '@tanstack/react-query'
import { Alert } from '../../components/common/Alert'
import { Button } from '../../components/common/Button'
import { EmptyState } from '../../components/common/EmptyState'
import { StatusBadge, type BadgeTone } from '../../components/common/StatusBadge'
import { formatNumber } from '../../lib/format'
import { queryKeys } from '../../lib/query-keys'
import { useConfirm } from '../../lib/use-confirm'
import { cn } from '../../lib/utils'
import {
  isActiveJob, useCancelExplanationJob, useExplanationAction, useExplanationDocuments, useExplanationEstimate, useExplanationJobs,
  useExplanationQuestions, useExplanationStatus, useSaveExplanation, useStartExplanationJob,
  type AdminExplanationQuestion, type ExplanationCounts, type ExplanationFilter, type ExplanationJob, type ExplanationQuestionType, type ExplanationStatus,
} from './api'

const card = 'rounded-xl border border-slate-200 bg-white'
const field = 'h-10 w-full min-w-0 rounded-xl border border-slate-300 bg-white px-3 text-sm focus:border-indigo-500 focus:outline-none focus:ring-2 focus:ring-indigo-500/20'
const usd = (value: number) => `$${value < 0.01 && value > 0 ? value.toFixed(4) : value.toFixed(2)}`

const statusBadge: Record<ExplanationStatus, { tone: BadgeTone; label: string }> = {
  Draft: { tone: 'info', label: 'Qaralama' },
  Review: { tone: 'warning', label: 'Yoxlanmalı' },
  Approved: { tone: 'success', label: 'Təsdiqlənib' },
  Rejected: { tone: 'danger', label: 'Rədd edilib' },
}

const jobLabel: Record<ExplanationJob['status'], string> = {
  Pending: 'Növbədədir', Running: 'Hazırlanır', Completed: 'Tamamlandı', Failed: 'Dayandı (xəta)', Cancelled: 'Ləğv edildi', BudgetExhausted: 'Aylıq limit doldu',
}

const errorLabel: Record<string, string> = {
  budget: 'Aylıq xərc limiti doldu.', notconfigured: 'OpenAI açarı və ya model konfiqurasiyası yanlışdır.', quotaexceeded: 'OpenAI balansı bitib.',
  ratelimited: 'OpenAI sorğu limiti.', unavailable: 'OpenAI əlçatan deyil.', timeout: 'Cavab vaxtında gəlmədi.', rejected: 'Sorğu rədd edildi.',
  'invalid-output': 'AI cavabı yararsız idi.', 'invalid-item': 'Bəzi suallar üçün AI cavabı yararsız idi.', unexpected: 'Gözlənilməz xəta.',
}

const filters: { value: ExplanationFilter; label: string; count: (c: ExplanationCounts) => number }[] = [
  { value: 'all', label: 'Hamısı', count: c => c.total },
  { value: 'missing', label: 'İzahsız', count: c => c.missing },
  { value: 'draft', label: 'Qaralama', count: c => c.draft },
  { value: 'review', label: 'Yoxlanmalı', count: c => c.review },
  { value: 'approved', label: 'Təsdiqlənib', count: c => c.approved },
  { value: 'rejected', label: 'Rədd edilib', count: c => c.rejected },
  { value: 'failed', label: 'Uğursuz', count: c => c.failed },
]

export function ExplanationBankPage() {
  const documents = useExplanationDocuments()
  const [documentId, setDocumentId] = useState('')
  const selectId = useId()

  return <div className="space-y-6">
    <AiStatusBar />
    <section className={cn(card, 'p-4')}>
      <label htmlFor={selectId} className="mb-2 block text-sm font-medium">PDF</label>
      <select id={selectId} value={documentId} onChange={e => setDocumentId(e.target.value)} className={field} disabled={documents.isPending}>
        <option value="">{documents.isPending ? 'Yüklənir…' : 'Sənəd seçin'}</option>
        {documents.data?.map(d => <option key={d.id} value={d.id}>{d.name} · {formatNumber(d.questions)} sual</option>)}
      </select>
      {documents.error && <div className="mt-3"><Alert title="Sənədlər yüklənmədi">{documents.error.message}</Alert></div>}
    </section>
    {documentId
      ? <DocumentExplanations key={documentId} documentId={documentId} />
      : <EmptyState icon={<FileText className="size-6" />} title="Sənəd seçin" description="İzahları görmək və hazırlamaq üçün yuxarıdan PDF seçin." />}
  </div>
}

function AiStatusBar() {
  const status = useExplanationStatus()
  if (status.isPending) return <div className={cn(card, 'h-16 animate-pulse')} />
  if (status.error) return <Alert title="AI vəziyyəti yüklənmədi">{status.error.message}</Alert>
  const s = status.data
  const used = s.monthlyBudgetUsd > 0 ? Math.min(100, (s.monthSpentUsd / s.monthlyBudgetUsd) * 100) : 100
  return <section aria-label="AI vəziyyəti" className={cn(card, 'flex flex-wrap items-center gap-x-6 gap-y-3 p-4 text-sm')}>
    <div className="flex items-center gap-2">
      <StatusBadge tone={!s.enabled ? 'neutral' : s.realCalls ? 'success' : 'info'}>{!s.enabled ? 'Deaktiv' : s.provider === 'Mock' ? 'Mock rejim' : 'OpenAI'}</StatusBadge>
      <span className="text-slate-500">{s.theoryModel} · {s.mathModel}</span>
    </div>
    <div className="min-w-48 flex-1">
      <div className="mb-1 flex justify-between text-xs text-slate-500"><span>Bu ay</span><span className="tabular-nums">{usd(s.monthSpentUsd)} / {usd(s.monthlyBudgetUsd)}</span></div>
      <div className="h-1.5 overflow-hidden rounded-full bg-slate-100" role="progressbar" aria-label="Aylıq xərc" aria-valuemin={0} aria-valuemax={100} aria-valuenow={Math.round(used)}>
        <div className={cn('h-full rounded-full', used >= 90 ? 'bg-rose-500' : 'bg-indigo-500')} style={{ width: `${used}%` }} />
      </div>
    </div>
    <span className="text-xs text-slate-500">Bir dəfəyə ən çox {s.maxQuestionsPerJob} sual</span>
  </section>
}

function DocumentExplanations({ documentId }: { documentId: string }) {
  const [filter, setFilter] = useState<ExplanationFilter>('all')
  const [term, setTerm] = useState('')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [notice, setNotice] = useState('')
  const questions = useExplanationQuestions(documentId, filter, search, page)
  const action = useExplanationAction(documentId)
  const confirm = useConfirm()
  useEffect(() => { const t = setTimeout(() => { setSearch(term); setPage(1) }, 300); return () => clearTimeout(t) }, [term])
  useEffect(() => { if (!notice) return; const t = setTimeout(() => setNotice(''), 4000); return () => clearTimeout(t) }, [notice])

  const drafts = questions.data?.items.filter(i => i.explanation?.status === 'Draft').map(i => i.explanation!.id) ?? []
  const pages = questions.data ? Math.max(1, Math.ceil(questions.data.totalCount / questions.data.pageSize)) : 1

  async function approveVisible() {
    const ok = await confirm({ title: `${drafts.length} qaralama təsdiqlənsin?`,
      description: 'Yalnız bu səhifədə görünən “Qaralama” izahlar təsdiqlənir. “Yoxlanmalı” izahlar ayrıca baxılmalıdır və toplu təsdiqə düşmür. Təsdiqlənən izahlar istifadəçilərə görünəcək.',
      confirmLabel: 'Təsdiqlə' })
    if (ok) action.mutate({ kind: 'approve-many', ids: drafts }, { onSuccess: r => setNotice(`${(r as { approved: number }).approved} izah təsdiqləndi.`) })
  }

  return <>
    <GenerationPanel documentId={documentId} onShowFailed={() => { setFilter('failed'); setPage(1) }} />
    <section aria-labelledby="explanations-heading" className="space-y-3">
      <div className="flex flex-wrap items-center gap-3">
        <h2 id="explanations-heading" className="font-semibold">Suallar və izahlar</h2>
        {drafts.length > 0 && <Button variant="secondary" className="ml-auto h-9 px-3 text-xs" disabled={action.isPending} onClick={approveVisible}>
          <Check className="size-3.5" aria-hidden />Görünən qaralamaları təsdiqlə ({drafts.length})</Button>}
      </div>
      <div role="tablist" aria-label="İzah filtri" className="flex flex-wrap gap-1.5">
        {filters.map(f => <button key={f.value} type="button" role="tab" aria-selected={filter === f.value} onClick={() => { setFilter(f.value); setPage(1) }}
          className={cn('rounded-lg border px-3 py-1.5 text-xs font-medium transition-colors', filter === f.value ? 'border-indigo-300 bg-indigo-50 text-indigo-700' : 'border-slate-200 bg-white text-slate-600 hover:bg-slate-50')}>
          {f.label} <span className="tabular-nums text-slate-400">{questions.data ? f.count(questions.data.counts) : '·'}</span></button>)}
      </div>
      <input type="search" aria-label="Sual mətnində və ya nömrədə axtar" placeholder="Sual mətni və ya nömrəsi…" value={term} onChange={e => setTerm(e.target.value)} maxLength={200} className={field} />
      <div aria-live="polite" className="space-y-2">
        {notice && <Alert tone="info" title="Uğurlu">{notice}</Alert>}
        {action.error && <Alert title="Əməliyyat alınmadı">{action.error.message}</Alert>}
      </div>
      {questions.isPending ? [0, 1, 2].map(i => <div key={i} className={cn(card, 'h-36 animate-pulse')} />)
        : questions.error ? <Alert title="Suallar yüklənmədi">{questions.error.message}</Alert>
        : questions.data.items.length === 0 ? <EmptyState title="Bu filtrə uyğun sual yoxdur" />
        : <ul className={cn('space-y-3', questions.isFetching && 'opacity-70')}>{questions.data.items.map(q =>
            <QuestionRow key={q.questionId} question={q} documentId={documentId} busy={action.isPending}
              onAction={(kind, id) => action.mutate({ kind, id })} />)}</ul>}
      {questions.data && pages > 1 && <nav aria-label="Səhifələr" className="flex items-center justify-center gap-3 pt-2">
        <Button variant="secondary" disabled={page <= 1 || questions.isFetching} onClick={() => setPage(p => p - 1)}>Əvvəlki</Button>
        <span className="text-sm text-slate-500 tabular-nums">{page} / {pages}</span>
        <Button variant="secondary" disabled={page >= pages || questions.isFetching} onClick={() => setPage(p => p + 1)}>Növbəti</Button>
      </nav>}
    </section>
  </>
}

function GenerationPanel({ documentId, onShowFailed }: { documentId: string; onShowFailed: () => void }) {
  const status = useExplanationStatus()
  const estimate = useExplanationEstimate(documentId)
  const jobs = useExplanationJobs(documentId)
  const start = useStartExplanationJob(documentId)
  const cancel = useCancelExplanationJob(documentId)
  const confirm = useConfirm()
  const client = useQueryClient()
  const job = jobs.data?.[0]
  const active = isActiveJob(job)

  // When a run ends, refresh the question list, counts and estimate once.
  const wasActive = useRef(active)
  useEffect(() => {
    if (wasActive.current && !active) void client.invalidateQueries({ queryKey: queryKeys.explanations.document(documentId) })
    wasActive.current = active
  }, [active, client, documentId])

  const e = estimate.data
  const s = status.data
  const blocked = !s?.enabled ? 'AI generasiyası serverdə deaktivdir.'
    : s.provider === 'OpenAI' && !s.apiKeyConfigured ? 'OpenAI açarı serverdə konfiqurasiya edilməyib.'
      : !e ? null : e.pending === 0 ? 'Bu sənədin bütün izah edilə bilən sualları üçün izah var.'
        : !e.withinBudget ? `Təxmini xərc aylıq limitin qalığından (${usd(e.remainingUsd)}) çoxdur.` : null

  async function generate() {
    if (!e || !s) return
    const real = s.provider === 'OpenAI'
    const ok = await confirm({
      title: real ? 'Real OpenAI sorğuları göndərilsin?' : 'Mock generasiya başladılsın?',
      description: `${e.estimate.questions} sual (${e.estimate.theoryQuestions} nəzəri, ${e.estimate.mathQuestions} riyazi), ${e.estimate.requests} sorğu, ~${formatNumber(e.estimate.inputTokens + e.estimate.outputTokens)} token. `
        + `Təxmini xərc: ${usd(e.estimate.estimatedUsd)}${real ? '' : ' (mock rejimdə real ödəniş yoxdur, yalnız simulyasiya)'}. Nəticələr qaralama kimi saxlanılır və təsdiqinizi gözləyir.`,
      confirmLabel: real ? 'Bəli, göndər' : 'Başlat',
    })
    if (ok) start.mutate(e.estimate.estimatedUsd)
  }

  return <section aria-label="AI generasiyası" className={cn(card, 'space-y-4 p-4 sm:p-5')}>
    <div className="flex flex-wrap items-start justify-between gap-3">
      <div className="min-w-0">
        <h2 className="flex items-center gap-2 font-semibold"><Sparkles className="size-4 text-indigo-600" aria-hidden />AI ilə izahlar</h2>
        {estimate.isPending ? <p className="mt-1 text-sm text-slate-500">Hesablanır…</p>
          : e && <p className="mt-1 text-sm text-slate-500">
            İzahsız: <b className="text-slate-800 tabular-nums">{formatNumber(e.pending)}</b> sual · növbəti dəfə {e.estimate.questions} sual,{' '}
            {e.estimate.requests} sorğu, ~{formatNumber(e.estimate.inputTokens + e.estimate.outputTokens)} token, təxminən <b className="text-slate-800">{usd(e.estimate.estimatedUsd)}</b>
          </p>}
      </div>
      {active && job
        ? <Button variant="secondary" disabled={cancel.isPending} onClick={() => cancel.mutate(job.id)}><X className="size-4" aria-hidden />Dayandır</Button>
        : <Button onClick={generate} loading={start.isPending} disabled={!!blocked || !e || estimate.isFetching}><Sparkles className="size-4" aria-hidden />AI ilə izahları hazırla</Button>}
    </div>
    {blocked && !active && <p className="text-sm text-slate-500">{blocked}</p>}
    {start.error && <Alert title="Generasiya başlamadı">{start.error.message}</Alert>}
    {cancel.error && <Alert title="Dayandırılmadı">{cancel.error.message}</Alert>}
    {estimate.error && <Alert title="Təxmin hesablanmadı">{estimate.error.message}</Alert>}
    {job && <JobProgress job={job} onShowFailed={onShowFailed} />}
  </section>
}

function JobProgress({ job, onShowFailed }: { job: ExplanationJob; onShowFailed: () => void }) {
  const processed = job.done + job.failed
  const percent = job.total > 0 ? Math.round((processed / job.total) * 100) : 100
  const active = isActiveJob(job)
  return <div className="rounded-lg bg-slate-50 p-3 text-sm">
    <div className="mb-2 flex flex-wrap items-center gap-x-4 gap-y-1">
      <span className="flex items-center gap-1.5 font-medium">{active && <Loader2 className="size-3.5 animate-spin" aria-hidden />}{jobLabel[job.status]}</span>
      <span className="tabular-nums text-slate-500">{processed} / {job.total}</span>
      <span className="tabular-nums text-slate-500">{job.requests} sorğu · {formatNumber(job.inputTokens + job.outputTokens)} token · {usd(job.spentUsd)}</span>
      {job.provider === 'Mock' && <StatusBadge tone="info">Mock</StatusBadge>}
    </div>
    <div className="flex h-2 overflow-hidden rounded-full bg-slate-200" role="progressbar" aria-label="Generasiya" aria-valuemin={0} aria-valuemax={job.total} aria-valuenow={processed}>
      <div className="h-full bg-emerald-500 transition-[width] duration-300" style={{ width: `${job.total ? (job.done / job.total) * 100 : 0}%` }} />
      <div className="h-full bg-rose-400 transition-[width] duration-300" style={{ width: `${job.total ? (job.failed / job.total) * 100 : 0}%` }} />
    </div>
    <span className="sr-only">{percent}%</span>
    {(job.failed > 0 || (job.lastError && !active)) && <p className="mt-2 flex flex-wrap items-center gap-2 text-rose-700">
      {job.lastError && (errorLabel[job.lastError] ?? job.lastError)}
      {job.failed > 0 && <button type="button" onClick={onShowFailed} className="font-medium underline">{job.failed} uğursuz sualı göstər</button>}
    </p>}
  </div>
}

function QuestionRow({ question: q, documentId, busy, onAction }: {
  question: AdminExplanationQuestion; documentId: string; busy: boolean; onAction: (kind: 'approve' | 'reject' | 'delete', id: string) => void
}) {
  const [editing, setEditing] = useState(false)
  const e = q.explanation
  const confirm = useConfirm()

  async function regenerate() {
    if (!e) return
    const ok = await confirm({ title: 'İzah silinib yenidən yaradılsın?', description: 'Bu izah silinir və sual növbəti AI generasiyasına daxil olur.', confirmLabel: 'Sil', danger: true })
    if (ok) onAction('delete', e.id)
  }

  return <li className={cn(card, 'p-4 sm:p-5')}>
    <header className="flex items-start gap-3">
      <span className="mt-0.5 shrink-0 rounded-md bg-slate-100 px-2 py-0.5 text-xs font-semibold text-slate-600 tabular-nums">#{q.sourceNumber}</span>
      <h3 className="min-w-0 flex-1 text-[15px] leading-6 font-medium whitespace-pre-wrap wrap-anywhere">{q.text}</h3>
    </header>
    <ul className="mt-3 grid gap-1 text-sm sm:grid-cols-2">
      {q.options.map(o => <li key={o.label} className={cn('flex min-w-0 gap-2 rounded-lg px-2 py-1', o.isCorrect ? 'bg-emerald-50 font-medium text-emerald-900' : 'text-slate-600')}>
        <span className="shrink-0 font-semibold">{o.label})</span><span className="min-w-0 wrap-anywhere">{o.text}</span></li>)}
    </ul>

    <div className="mt-4 border-t border-slate-100 pt-3">
      {editing ? <ExplanationEditor question={q} documentId={documentId} onDone={() => setEditing(false)} />
        : !q.explainable ? <p className="text-sm text-slate-500">Düzgün cavab məlum deyil — izah hazırlanmır.</p>
        : !e ? <div className="flex flex-wrap items-center gap-3">
            <p className="text-sm text-slate-500">{q.failedLastRun ? 'Son generasiyada AI cavabı yararsız oldu.' : 'İzah hələ yoxdur.'}</p>
            <Button variant="ghost" className="h-9 px-3 text-xs" onClick={() => setEditing(true)}><Pencil className="size-3.5" aria-hidden />Əl ilə yaz</Button>
          </div>
        : <>
          <div className="mb-2 flex flex-wrap items-center gap-2 text-xs text-slate-500">
            <StatusBadge tone={statusBadge[e.status].tone}>{statusBadge[e.status].label}</StatusBadge>
            <span className="rounded-full bg-slate-100 px-2 py-0.5">{e.questionType === 'Math' ? 'Riyazi' : 'Nəzəri'}</span>
            {e.shared && <span className="rounded-full bg-indigo-50 px-2 py-0.5 text-indigo-700">Başqa sənəddən təkrar istifadə</span>}
            <span>{e.model ? `${e.model} · ${usd(e.costUsd)}` : 'Əl ilə yazılıb'}</span>
          </div>
          {e.reviewReason && <p className="mb-2 rounded-lg bg-amber-50 px-3 py-2 text-sm text-amber-800">{e.reviewReason}</p>}
          <p className="rounded-lg bg-slate-50 px-3 py-2 text-sm leading-6 whitespace-pre-wrap wrap-anywhere text-slate-700">{e.explanation}</p>
          <div className="mt-3 flex flex-wrap justify-end gap-2">
            {e.status !== 'Approved' && <Button variant="ghost" className="h-9 px-3 text-xs" disabled={busy} onClick={regenerate}><RotateCcw className="size-3.5" aria-hidden />Yenidən yarat</Button>}
            {e.status !== 'Rejected' && <Button variant="ghost" className="h-9 px-3 text-xs text-rose-600" disabled={busy} onClick={() => onAction('reject', e.id)}><X className="size-3.5" aria-hidden />Rədd et</Button>}
            <Button variant="secondary" className="h-9 px-3 text-xs" disabled={busy} onClick={() => setEditing(true)}><Pencil className="size-3.5" aria-hidden />Redaktə et</Button>
            {e.status !== 'Approved' && <Button className="h-9 px-3 text-xs" disabled={busy} onClick={() => onAction('approve', e.id)}><Check className="size-3.5" aria-hidden />Təsdiqlə</Button>}
          </div>
        </>}
    </div>
  </li>
}

function ExplanationEditor({ question, documentId, onDone }: { question: AdminExplanationQuestion; documentId: string; onDone: () => void }) {
  const current = question.explanation
  const [text, setText] = useState(current?.explanation ?? '')
  const [type, setType] = useState<ExplanationQuestionType>(current?.questionType ?? 'Theory')
  const save = useSaveExplanation(documentId)
  const textId = useId()
  const typeId = useId()
  const valid = text.trim().length >= 20 && text.trim().length <= 4000
  const submit = (approve: boolean) => save.mutate({ questionId: question.questionId, explanation: text, questionType: type, rowVersion: current?.rowVersion ?? null, approve }, { onSuccess: onDone })

  return <form className="space-y-3" onSubmit={ev => { ev.preventDefault(); submit(false) }}>
    <div className="grid gap-3 sm:grid-cols-[1fr_10rem]">
      <label htmlFor={textId} className="sr-only">İzah mətni</label>
      <textarea id={textId} value={text} onChange={ev => setText(ev.target.value)} rows={5} maxLength={4000} autoFocus
        placeholder="İzah (2–5 cümlə; riyazi düsturlar $...$ ilə)" className="w-full rounded-xl border border-slate-300 bg-white p-3 text-sm focus:border-indigo-500 focus:outline-none focus:ring-2 focus:ring-indigo-500/20" />
      <div>
        <label htmlFor={typeId} className="mb-1 block text-xs font-medium text-slate-500">Növ</label>
        <select id={typeId} value={type} onChange={ev => setType(ev.target.value as ExplanationQuestionType)} className={field}>
          <option value="Theory">Nəzəri</option><option value="Math">Riyazi</option>
        </select>
        <p className="mt-2 text-xs text-slate-400 tabular-nums">{text.trim().length} / 4000</p>
      </div>
    </div>
    {save.error && <Alert title="Saxlanmadı">{save.error.message}</Alert>}
    <div className="flex flex-wrap justify-end gap-2">
      <Button variant="ghost" className="h-9 px-3 text-xs" disabled={save.isPending} onClick={onDone}>Ləğv et</Button>
      <Button type="submit" variant="secondary" className="h-9 px-3 text-xs" disabled={!valid || save.isPending}>Qaralama kimi saxla</Button>
      <Button className="h-9 px-3 text-xs" disabled={!valid || save.isPending} loading={save.isPending} onClick={() => submit(true)}><Check className="size-3.5" aria-hidden />Saxla və təsdiqlə</Button>
    </div>
  </form>
}
