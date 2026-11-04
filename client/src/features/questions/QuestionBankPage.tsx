import { useEffect, useId, useRef, useState } from 'react'
import { Bookmark, Check, ChevronDown, FileText, Library, Pencil, ShieldCheck, X } from 'lucide-react'
import { Alert } from '../../components/common/Alert'
import { Button } from '../../components/common/Button'
import { EmptyState } from '../../components/common/EmptyState'
import { PageHeader } from '../../components/common/PageHeader'
import { StatusBadge, type BadgeTone } from '../../components/common/StatusBadge'
import { cn } from '../../lib/utils'
import type { QuestionDto, VerificationStatus } from '../documents/api'
import { BANK_PAGE_SIZE, useBankDocuments, useQuestionAction, useQuestionBank, useQuestionStats, type BankFilters } from './api'

const statusBadge: Record<VerificationStatus, { tone: BadgeTone; label: string }> = {
  Verified: { tone: 'success', label: 'Yoxlanılıb' },
  AutoDetected: { tone: 'info', label: 'Yoxlanılmayıb' },
  Unverified: { tone: 'warning', label: 'Cavab bilinmir' },
}
const card = 'rounded-xl border border-slate-200 bg-white shadow-sm'
const field = 'h-10 w-full min-w-0 rounded-xl border border-slate-300 bg-white px-3 text-sm focus:border-indigo-500 focus:outline-none focus:ring-2 focus:ring-indigo-500/20'
const initial: BankFilters = { search: '', documentId: '', type: '', status: '', bookmarked: false }

export function QuestionBankPage() {
  const [filters, setFilters] = useState(initial)
  const [term, setTerm] = useState('')
  const [page, setPage] = useState(1)
  const [editing, setEditing] = useState<QuestionDto | null>(null)
  const [notice, setNotice] = useState('')
  const stats = useQuestionStats()
  const documents = useBankDocuments()
  const selected = filters.documentId !== ''
  const bank = useQuestionBank(filters, page, selected)
  const action = useQuestionAction()
  const update = (patch: Partial<BankFilters>) => { setFilters(f => ({ ...f, ...patch })); setPage(1) }
  // Clearing the document hides the other filters, so reset them instead of applying them invisibly.
  const selectDocument = (documentId: string) => { if (!documentId) setTerm(''); update(documentId ? { documentId } : initial) }
  useEffect(() => { const timer = setTimeout(() => { setFilters(f => f.search === term ? f : { ...f, search: term }); setPage(1) }, 300); return () => clearTimeout(timer) }, [term])
  useEffect(() => { if (!notice) return; const timer = setTimeout(() => setNotice(''), 3000); return () => clearTimeout(timer) }, [notice])
  const pages = bank.data ? Math.max(1, Math.ceil(bank.data.totalCount / BANK_PAGE_SIZE)) : 1

  return <>
    <PageHeader title="Sual bankı" description="Sənədlərindən və səninlə paylaşılan materiallardan çıxarılmış suallar." />

    {selected && <section aria-label="Statistika" className="mb-6 grid grid-cols-3 gap-2 sm:gap-4">
      {[['Ümumi sual', stats.data?.totalQuestions], ['Sənəd', stats.data?.documentCount], ['Seçilmiş suallar', stats.data?.bookmarkCount]].map(([label, value]) =>
        <div key={label} className={cn(card, 'p-3 sm:p-5')}><p className="text-xs text-slate-500 sm:text-sm">{label}</p>
          {stats.isPending ? <div className="mt-1 h-7 w-12 animate-pulse rounded-lg bg-slate-100" /> : <p className="text-xl font-semibold tabular-nums sm:text-2xl">{value ?? '—'}</p>}</div>)}
    </section>}

    <section aria-label="Filtrlər" className={cn(card, 'grid grid-cols-1 gap-3 p-4', selected && 'sm:grid-cols-2 lg:grid-cols-[minmax(0,1.5fr)_minmax(0,2fr)_minmax(0,1fr)_auto]')}>
      <select aria-label="Sənəd" value={filters.documentId} onChange={e => selectDocument(e.target.value)} className={field}>
        <option value="">Sənəd seçin</option>{documents.data?.items.map(d => <option key={d.id} value={d.id}>{d.originalFileName}</option>)}
      </select>
      {selected && <>
      <input type="search" aria-label="Sual mətnində axtar" placeholder="Sual mətnində axtar…" value={term} onChange={e => setTerm(e.target.value)} maxLength={200} className={field} />
      <select aria-label="Yoxlanılma statusu" value={filters.status} onChange={e => update({ status: e.target.value as BankFilters['status'] })} className={field}>
        <option value="">Bütün statuslar</option><option value="Verified">Yoxlanılıb</option><option value="AutoDetected">Yoxlanılmayıb</option><option value="Unverified">Cavab bilinmir</option>
      </select>
      <button type="button" aria-pressed={filters.bookmarked} onClick={() => update({ bookmarked: !filters.bookmarked })} className={cn('flex h-10 items-center justify-center gap-2 rounded-xl border px-3 text-sm font-medium transition-colors', filters.bookmarked ? 'border-indigo-300 bg-indigo-50 text-indigo-700' : 'border-slate-300 text-slate-600 hover:bg-slate-50')}>
        <Bookmark className={cn('size-4', filters.bookmarked && 'fill-current')} aria-hidden />Seçilmişlər</button>
      </>}
    </section>

    <div className="mt-4 space-y-3" aria-live="polite">
      {notice && <Alert tone="info" title="Uğurlu">{notice}</Alert>}
      {action.error && !editing && <Alert title="Əməliyyat alınmadı">{action.error.message}</Alert>}
    </div>

    <div className="mt-4 space-y-4">
      {!selected ? <EmptyState icon={<FileText className="size-6" />} title="Sənəd seçin" description="Sualları görmək üçün yuxarıdan sənəd seçin." />
        : bank.isPending ? [0, 1, 2].map(i => <div key={i} className={cn(card, 'h-40 animate-pulse')} />)
        : bank.error ? <Alert title="Suallar yüklənmədi">{bank.error.message}</Alert>
        : bank.data.items.length === 0 ? <EmptyState icon={<Library className="size-6" />} title="Sual tapılmadı" description={JSON.stringify(filters) === JSON.stringify(initial) ? 'Sənəd yüklədikdə çıxarılan suallar burada görünəcək.' : 'Axtarışı və ya filtrləri dəyişin.'} />
        : bank.data.items.map(q => <QuestionItem key={q.id} question={q} busy={action.isPending}
            onBookmark={() => action.mutate({ kind: 'bookmark', id: q.id, on: !q.isBookmarked }, { onSuccess: () => setNotice(q.isBookmarked ? 'Seçilmişlərdən çıxarıldı.' : 'Seçilmişlərə əlavə edildi.') })}
            onVerify={() => action.mutate({ kind: 'verify', id: q.id }, { onSuccess: () => setNotice('Sual yoxlanılmış kimi təsdiqləndi.') })}
            onEdit={() => { action.reset(); setEditing(q) }} />)}
    </div>

    {bank.data && bank.data.totalCount > BANK_PAGE_SIZE && <nav aria-label="Səhifələr" className="mt-6 flex items-center justify-center gap-3">
      <Button variant="secondary" disabled={page <= 1 || bank.isFetching} onClick={() => setPage(p => p - 1)}>Əvvəlki</Button>
      <span className="text-sm text-slate-500 tabular-nums">{page} / {pages}</span>
      <Button variant="secondary" disabled={page >= pages || bank.isFetching} onClick={() => setPage(p => p + 1)}>Növbəti</Button>
    </nav>}

    {editing && <EditModal question={editing} onClose={() => setEditing(null)} onSaved={() => { setEditing(null); setNotice('Sual yadda saxlanıldı.') }} />}
  </>
}

function QuestionItem({ question: q, busy, onBookmark, onVerify, onEdit }: { question: QuestionDto; busy: boolean; onBookmark: () => void; onVerify: () => void; onEdit: () => void }) {
  const [open, setOpen] = useState(false)
  const badge = statusBadge[q.verificationStatus]
  const isOpen = q.options.length === 0
  const long = (q.answer?.length ?? 0) > 280
  return <article className={cn(card, 'p-4 transition-shadow hover:shadow-md sm:p-5')}>
    <header className="flex items-start gap-3">
      <span className="mt-0.5 shrink-0 rounded-md bg-slate-100 px-2 py-0.5 text-xs font-semibold text-slate-600 tabular-nums">#{q.sourceNumber}</span>
      <h3 className="min-w-0 flex-1 wrap-anywhere text-[15px] leading-6 font-medium">{q.text}</h3>
      <button type="button" disabled={busy} onClick={onBookmark} aria-pressed={q.isBookmarked} aria-label={q.isBookmarked ? 'Seçilmişlərdən çıxar' : 'Seçilmişlərə əlavə et'} className="-m-1 grid size-10 shrink-0 place-items-center rounded-lg text-slate-400 transition-colors hover:bg-indigo-50 hover:text-indigo-600 disabled:opacity-50 sm:m-0 sm:size-8">
        <Bookmark className={cn('size-5', q.isBookmarked && 'fill-indigo-600 text-indigo-600')} aria-hidden /></button>
    </header>
    <div className="mt-2 mb-3 flex flex-wrap items-center gap-2 text-xs text-slate-500">
      <StatusBadge tone={badge.tone}>{badge.label}</StatusBadge>
      <span className="rounded-full bg-slate-100 px-2 py-0.5">{isOpen ? 'Açıq sual' : 'Variantlı'}</span>
      <span className="flex min-w-0 max-w-full items-center gap-1"><FileText className="size-3.5 shrink-0" aria-hidden /><span className="truncate">{q.documentName}</span></span>
    </div>
    {isOpen ? <div className="rounded-lg bg-slate-50 px-3 py-2 text-sm leading-6 text-slate-700">
        <p className={cn('whitespace-pre-wrap wrap-anywhere', long && !open && 'line-clamp-4')}>{q.answer || 'Cavab yoxdur.'}</p>
        {long && <button type="button" onClick={() => setOpen(o => !o)} aria-expanded={open} className="mt-1 flex items-center gap-1 text-xs font-medium text-indigo-600">
          {open ? 'Bağla' : 'Tam cavabı göstər'}<ChevronDown className={cn('size-3.5 transition-transform', open && 'rotate-180')} aria-hidden /></button>}
      </div>
      : <ul className="flex flex-col gap-1.5">{q.options.map(o => <li key={o.id} className={cn('flex items-start gap-3 rounded-lg border px-3 py-2', o.isCorrect ? 'border-emerald-200 bg-emerald-50' : 'border-transparent')}>
        <span className={cn('flex size-6 shrink-0 items-center justify-center rounded-full text-xs font-semibold', o.isCorrect ? 'bg-emerald-600 text-white' : 'bg-slate-100 text-slate-600')} aria-hidden>{o.isCorrect ? <Check className="size-3.5" strokeWidth={3} /> : o.label}</span>
        <span className={cn('min-w-0 flex-1 wrap-anywhere leading-6', o.isCorrect ? 'font-medium text-emerald-900' : 'text-slate-700')}><span className="sr-only">{o.label}){o.isCorrect ? ' (düzgün cavab)' : ''} </span>{o.text}</span>
      </li>)}</ul>}
    {q.canEdit && <footer className="mt-4 flex flex-wrap justify-end gap-2 border-t border-slate-100 pt-3">
      {q.verificationStatus !== 'Verified' && <Button variant="ghost" className="h-9 px-3 text-xs" disabled={busy} onClick={onVerify}><ShieldCheck className="size-3.5" aria-hidden />Təsdiqlə</Button>}
      <Button variant="secondary" className="h-9 px-3 text-xs" disabled={busy} onClick={onEdit}><Pencil className="size-3.5" aria-hidden />Redaktə et</Button>
    </footer>}
  </article>
}

function EditModal({ question, onClose, onSaved }: { question: QuestionDto; onClose: () => void; onSaved: () => void }) {
  const ref = useRef<HTMLDialogElement>(null)
  const titleId = useId()
  const radio = useId()
  const action = useQuestionAction()
  const [text, setText] = useState(question.text)
  const [answer, setAnswer] = useState(question.answer ?? '')
  const [options, setOptions] = useState(question.options.map(o => ({ id: o.id, label: o.label, text: o.text, isCorrect: o.isCorrect })))
  useEffect(() => { const dialog = ref.current; dialog?.showModal(); return () => dialog?.close() }, [])
  const isOpen = options.length === 0
  const invalid = !text.trim() || (isOpen ? !answer.trim() : options.filter(o => o.isCorrect).length !== 1 || options.some(o => !o.text.trim()))
  const input = 'w-full rounded-xl border border-slate-300 bg-white p-3 text-sm focus:border-indigo-500 focus:outline-none focus:ring-2 focus:ring-indigo-500/20'
  return <dialog ref={ref} aria-labelledby={titleId} onCancel={e => { e.preventDefault(); if (!action.isPending) onClose() }}
    className="m-auto max-h-[85dvh] w-[calc(100%-2rem)] max-w-2xl overflow-y-auto rounded-xl border border-slate-200 bg-white p-5 shadow-xl backdrop:bg-slate-900/40 sm:p-6">
    <div className="mb-5 flex items-center justify-between gap-3"><h2 id={titleId} className="text-lg font-semibold">Sualı redaktə et · #{question.sourceNumber}</h2>
      <Button variant="ghost" className="size-9 p-0" disabled={action.isPending} onClick={onClose} aria-label="Bağla"><X className="size-4" aria-hidden /></Button></div>
    <form className="space-y-4" onSubmit={e => { e.preventDefault(); action.mutate({ kind: 'edit', id: question.id, edit: { text, answer: isOpen ? answer : null, options: options.map(({ id, text, isCorrect }) => ({ id, text, isCorrect })) } }, { onSuccess: onSaved }) }}>
      <label className="block"><span className="mb-1.5 block text-sm font-medium">Sual mətni</span><textarea value={text} onChange={e => setText(e.target.value)} rows={3} maxLength={4000} className={input} /></label>
      {isOpen ? <label className="block"><span className="mb-1.5 block text-sm font-medium">Cavab</span><textarea value={answer} onChange={e => setAnswer(e.target.value)} rows={6} maxLength={8000} className={input} /></label>
        : <fieldset className="space-y-2"><legend className="mb-1.5 text-sm font-medium">Variantlar · düzgün cavabı seçin</legend>
          {options.map((o, i) => <div key={o.id} className={cn('flex items-center gap-3 rounded-xl border p-2', o.isCorrect ? 'border-emerald-300 bg-emerald-50' : 'border-slate-200')}>
            <input type="radio" name={radio} checked={o.isCorrect} aria-label={`${o.label} düzgün cavabdır`} onChange={() => setOptions(list => list.map((x, j) => ({ ...x, isCorrect: j === i })))} className="size-4 accent-emerald-600" />
            <span className="w-5 text-sm font-semibold text-slate-500">{o.label}</span>
            <input value={o.text} aria-label={`${o.label} variantının mətni`} maxLength={1000} onChange={e => setOptions(list => list.map((x, j) => j === i ? { ...x, text: e.target.value } : x))} className="h-10 flex-1 rounded-lg border border-slate-300 bg-white px-3 text-sm focus:border-indigo-500 focus:outline-none" />
          </div>)}</fieldset>}
      <p className="text-xs text-slate-500">Yadda saxladıqda sual “Yoxlanılıb” statusu alır.</p>
      {action.error && <Alert title="Saxlanmadı">{action.error.message}</Alert>}
      <div className="flex justify-end gap-2"><Button variant="secondary" disabled={action.isPending} onClick={onClose}>Ləğv et</Button><Button type="submit" loading={action.isPending} disabled={invalid}>Yadda saxla</Button></div>
    </form>
  </dialog>
}
