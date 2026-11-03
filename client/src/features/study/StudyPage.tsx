import { useState } from 'react'
import { Link, useSearchParams } from 'react-router'
import { ArrowRight, BookOpen, Check, ListChecks, X } from 'lucide-react'
import { Alert } from '../../components/common/Alert'
import { Button } from '../../components/common/Button'
import { EmptyState } from '../../components/common/EmptyState'
import { formatDate, formatNumber } from '../../lib/format'
import { cn } from '../../lib/utils'
import { useConfirm } from '../../lib/use-confirm'
import { useDeletePractice, usePractice, usePracticeHistory, useStudyDocuments, type PracticeResult, type PracticeView, type StudyMode } from './api'

export function StudyPage() {
  const documents = useStudyDocuments()
  const [searchParams] = useSearchParams()
  const [documentId, setDocumentId] = useState(() => searchParams.get('document') ?? '')
  const document = documents.data?.find(d => d.id === documentId)
  return <div className="mx-auto max-w-4xl space-y-6">
    <header>
      <h1 className="flex items-center gap-2 text-2xl font-semibold"><BookOpen className="size-6 text-indigo-600" />Öyrənmə köməkçisi</h1>
      <p className="mt-2 text-slate-500">Sənədindəki açıq suallarla öyrən və variantlı məşqlərlə biliklərini yoxla.</p>
    </header>
    <section className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
      <label htmlFor="study-document" className="mb-3 block font-semibold">Öyrənəcəyin sənəd</label>
      <select id="study-document" disabled={documents.isPending || !!documents.error} value={document?.id ?? ''} onChange={e => setDocumentId(e.target.value)}
        className="h-11 w-full rounded-lg border border-slate-300 bg-white px-3 focus:border-indigo-500 focus:ring-2 focus:ring-indigo-500/20 focus:outline-none disabled:opacity-50">
        <option value="">{documents.isPending ? 'Yüklənir…' : 'PDF və ya DOCX seçin'}</option>
        {documents.data?.map(d => <option key={d.id} value={d.id}>{d.originalFileName} · {formatNumber(d.questionCount)} sual</option>)}
      </select>
      <p className="mt-2 text-xs text-slate-500">Yalnız özünə məxsus, açıq sualları və izahlı cavabları olan sənədlər göstərilir.</p>
      {documents.error && <div className="mt-3"><Alert title={documents.error.message} action={<Button variant="ghost" onClick={() => void documents.refetch()}>Yenilə</Button>} /></div>}
      {documents.data?.length === 0 && <div className="mt-4"><EmptyState title="Öyrənmək üçün sənəd yoxdur" description="Yazılı imtahan üçün hazırlanmış açıq sual-cavab sənədi yüklə." action={<Link to="/upload" className="text-indigo-600">Sənəd yüklə</Link>} /></div>}
    </section>
    {document && <PracticePanel key={document.id} documentId={document.id} questionCount={document.questionCount} />}
  </div>
}

function PracticePanel({ documentId, questionCount }: { documentId: string; questionCount: number }) {
  const practice = usePractice(documentId)
  const history = usePracticeHistory(documentId)
  const [mode, setMode] = useState<StudyMode | null>(null)
  const remove = useDeletePractice(documentId)
  const confirm = useConfirm()
  const [count, setCount] = useState(Math.min(10, questionCount, 30))
  const [draft, setDraft] = useState('')
  const [view, setView] = useState<PracticeView | null>(null)
  const busy = practice.isPending || remove.isPending
  const receive = (next: PracticeView) => { if (next.question?.id !== view?.question?.id || next.answer !== view?.answer) setDraft(next.answer ?? ''); setView(next); setMode(next.mode) }
  const start = (nextMode: StudyMode) => practice.mutate({ kind: 'start', mode: nextMode, count }, { onSuccess: receive })
  const action = (name: 'next' | 'hint' | 'explain' | 'reveal' | 'answer', optionIndex?: number) => {
    if (!view?.question || busy) return
    practice.mutate({ kind: 'action', id: view.id, questionId: view.question.id, action: name, optionIndex }, { onSuccess: receive })
  }
  const question = view?.question
  return <div className="space-y-5">
    <div className="grid gap-3 sm:grid-cols-2" aria-label="Öyrənmə rejimi">
      <button type="button" disabled={busy} aria-pressed={mode === 'open'} onClick={() => { setMode('open'); setView(null); practice.reset() }}
        className={cn('flex items-center gap-3 rounded-xl border p-5 text-left transition-colors disabled:opacity-50', mode === 'open' ? 'border-indigo-500 bg-indigo-50' : 'border-slate-200 bg-white hover:border-indigo-300')}>
        <BookOpen className="size-6 shrink-0 text-indigo-600" aria-hidden /><span><span className="block font-semibold">Məni yoxla</span><span className="mt-1 block text-sm text-slate-500">Təsadüfi açıq suallar və sənəddəki cavablar</span></span>
      </button>
      <button type="button" disabled={busy} aria-pressed={mode === 'mcq'} onClick={() => { setMode('mcq'); setView(null); practice.reset() }}
        className={cn('flex items-center gap-3 rounded-xl border p-5 text-left transition-colors disabled:opacity-50', mode === 'mcq' ? 'border-indigo-500 bg-indigo-50' : 'border-slate-200 bg-white hover:border-indigo-300')}>
        <ListChecks className="size-6 shrink-0 text-indigo-600" aria-hidden /><span><span className="block font-semibold">Variantlı sual</span><span className="mt-1 block text-sm text-slate-500">A/B/C/D məşqi, nəticə və zəif mövzular</span></span>
      </button>
    </div>
    {mode && !view && <section className="space-y-4 rounded-xl border border-slate-200 bg-white p-5">
      <label className="flex items-center gap-3 text-sm">Sual sayı<input type="number" min={1} max={Math.min(30, questionCount)} value={Number.isNaN(count) ? '' : count} disabled={busy} onChange={e => setCount(e.target.valueAsNumber)}
        className="h-10 w-24 rounded-lg border border-slate-300 px-3" /></label>
      <p className="text-xs text-slate-500">Ən çox {Math.min(30, questionCount)} sual. {mode === 'mcq' ? 'Variantlar sənəddəki cavablardan hazırlanır. Uyğun suallar azdırsa, say barədə məlumat veriləcək. Sual ən çox 240, hər variant 220 simvoldur.' : 'Seçdiyin sayda təsadüfi açıq sualla məşq et.'}</p>
      <Button loading={busy} disabled={!Number.isInteger(count) || count < 1 || count > Math.min(30, questionCount)} onClick={() => start(mode)}>Məşqə başla</Button>
    </section>}
    {practice.error && <Alert title={practice.error.message} action={view ? <Button variant="ghost" onClick={() => practice.mutate({ kind: 'load', id: view.id }, { onSuccess: receive })}>Məşqi yenilə</Button> : undefined} />}
    {busy && <p role="status" className="animate-pulse text-sm text-indigo-600">Hazırlanır…</p>}
    {view && !view.finished && question && <section className="overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm">
      <div className="flex flex-wrap items-center justify-between gap-2 border-b border-slate-100 px-5 py-3 text-sm text-slate-500">
        <span>{view.mode === 'open' ? 'Açıq sual' : 'Variantlı sual'}</span><span>{view.position} / {view.total}</span>
      </div>
      <div className="space-y-5 p-5 sm:p-6">
        <p className="text-xs font-medium text-indigo-600">Sənəddəki sual: {question.number}</p>
        <h2 className="whitespace-pre-wrap text-lg font-medium leading-8 text-slate-800">{question.text}</h2>
        {view.mode === 'open' ? <>
          {view.answer && <div role="status" className="rounded-xl bg-indigo-50 p-4 text-sm leading-7 text-slate-700"><p className="font-medium text-indigo-700">Cavabın saxlanıldı</p><p className="whitespace-pre-wrap">{view.answer}</p></div>}
          {view.evaluation && <div role="status" className="space-y-2 rounded-xl border border-indigo-200 bg-indigo-50 p-4 text-sm">
            <p className="font-semibold">{view.evaluation.verdict} · {view.evaluation.score}%</p>
            {!!view.evaluation.missingConcepts.length && <p>Çatışmayan anlayışlar: {view.evaluation.missingConcepts.join(', ')}</p>}
          </div>}
          <form className="space-y-3" onSubmit={event => {
            event.preventDefault()
            if (!draft.trim() || busy) return
            practice.mutate({ kind: 'action', id: view.id, questionId: question.id, action: 'respond', answer: draft }, { onSuccess: receive })
          }}>
            <label htmlFor="study-answer" className="block text-sm font-medium">Cavabın</label>
            <textarea id="study-answer" rows={4} maxLength={16000} value={draft} onChange={event => setDraft(event.target.value)} disabled={busy}
              placeholder="Bu suala cavabını yaz…" className="w-full rounded-xl border border-slate-300 p-3 text-sm leading-6 focus:border-indigo-500 focus:ring-2 focus:ring-indigo-500/20 focus:outline-none disabled:opacity-50" />
            <Button type="submit" disabled={busy || !draft.trim() || draft.trim() === view.answer}>Cavabı göndər</Button>
            <p className="text-xs text-slate-500">Qiymət sənəddəki əsas sözlərin uyğunluğuna görə təxmin edilir; mənanı və sinonimləri tam yoxlamır.</p>
          </form>
          <div className="flex flex-wrap gap-2">
            <Button variant="secondary" disabled={busy} onClick={() => action('hint')}>İpucu ver</Button>
            <Button variant="secondary" disabled={busy} onClick={() => action('explain')}>Sadə izah et</Button>
            <Button variant="secondary" disabled={busy} onClick={() => action('reveal')}>Doğru cavabı göstər</Button>
          </div>
          {view.help && <div aria-live="polite" className="whitespace-pre-wrap rounded-xl bg-slate-50 p-4 text-sm leading-7 text-slate-700">{view.help}</div>}
        </> : <>
          <div className="grid gap-3">{question.options.map((option, index) => {
            const feedback = view.feedback
            return <button key={index} type="button" disabled={busy || !!feedback} onClick={() => action('answer', index)}
              className={cn('flex items-start gap-3 rounded-xl border p-4 text-left text-sm leading-6 disabled:cursor-default',
                feedback && index === feedback.correctIndex ? 'border-emerald-300 bg-emerald-50 text-emerald-800'
                  : feedback && index === feedback.selectedIndex ? 'border-rose-300 bg-rose-50 text-rose-800' : 'border-slate-200 text-slate-700 hover:border-indigo-300')}>
              <span className="font-semibold">{String.fromCharCode(65 + index)}.</span><span>{option}</span>
            </button>
          })}</div>
          {view.feedback && <p role="status" className={cn('flex items-center gap-2 text-sm font-medium', view.feedback.correct ? 'text-emerald-700' : 'text-rose-700')}>
            {view.feedback.correct ? <Check className="size-4" aria-hidden /> : <X className="size-4" aria-hidden />}{view.feedback.correct ? 'Doğru cavab' : 'Yanlış cavab'}
          </p>}
        </>}
      </div>
      <footer className="flex justify-end border-t border-slate-100 px-5 py-4">
        <Button disabled={busy || (view.mode === 'mcq' && !view.feedback)} onClick={() => action('next')}>
          {view.position === view.total ? 'Məşqi tamamla' : 'Növbəti sual'}<ArrowRight className="size-4" aria-hidden />
        </Button>
      </footer>
    </section>}
    {view?.finished && (view.result ? <Results result={view.result} /> : <EmptyState title="Açıq sual məşqi tamamlandı" description="Yeni suallarla davam etmək üçün «Məni yoxla» seç." />)}
    {history.isPending && <p role="status" className="text-sm text-slate-500">Məşq tarixçəsi yüklənir…</p>}
    {history.error && <Alert title={history.error.message} />}
    {remove.error && <Alert title={remove.error.message} />}
    {!!history.data?.length && <section className="rounded-xl border border-slate-200 bg-white p-5">
      <h2 className="font-semibold">Məşqlərim</h2>
      <ul className="mt-4 divide-y divide-slate-100">{history.data.map(item => <li key={item.id} className="flex flex-wrap items-center justify-between gap-3 py-3">
        <div><p className="text-sm font-medium">{item.mode === 'open' ? 'Məni yoxla' : 'Variantlı sual'}{item.result ? ` · ${item.result.percent}%` : ''}</p><p className="mt-1 text-xs text-slate-500">{formatDate(item.createdAt)} · {item.total} sual · {item.finished ? 'Tamamlanıb' : 'Davam edir'}</p></div>
        <div className="flex gap-2">
          <Button variant="ghost" disabled={busy} onClick={() => practice.mutate({ kind: 'load', id: item.id }, { onSuccess: receive })}>{item.finished ? 'Nəticəyə bax' : 'Davam et'}</Button>
          <Button variant="ghost" className="text-rose-600" disabled={busy} onClick={async () => {
            if (!await confirm({ title: 'Məşq silinsin?', description: 'Bu məşq və ona bağlı cavablar silinəcək.', confirmLabel: 'Sil', danger: true })) return
            remove.mutate(item.id, { onSuccess: () => { if (view?.id === item.id) { setView(null); setDraft(''); practice.reset() } } })
          }}>Sil</Button>
        </div>
      </li>)}</ul>
    </section>}
  </div>
}

function Results({ result }: { result: PracticeResult }) {
  return <div className="space-y-5">
    <section className="rounded-xl border border-slate-200 bg-white p-5">
      <h2 className="text-lg font-semibold">Məşq nəticəsi</h2>
      <dl className="mt-4 grid grid-cols-3 gap-3 text-center">
        {[['Doğru', result.correct, 'text-emerald-700'], ['Yanlış', result.incorrect, 'text-rose-700'], ['Nəticə', `${result.percent}%`, 'text-indigo-700']].map(([label, value, color]) => <div key={label} className="rounded-xl bg-slate-50 p-4"><dt className="text-xs text-slate-500">{label}</dt><dd className={cn('mt-2 text-2xl font-semibold', String(color))}>{value}</dd></div>)}
      </dl>
      {!!result.mistakes.length && <div className="mt-5 space-y-3"><h3 className="font-medium">Yanlış cavabların izahı</h3>{result.mistakes.map((item, index) => <article key={item.questionId} className="rounded-xl border border-rose-100 bg-rose-50/50 p-4">
        <h4 className="text-sm font-semibold">{index + 1}. {item.topic}</h4><p className="mt-1 text-xs text-slate-500">Seçimin: {String.fromCharCode(65 + item.selectedIndex)} · Doğru: {String.fromCharCode(65 + item.correctIndex)}</p>
        <p className="mt-2 whitespace-pre-wrap text-sm leading-6 text-slate-700">{item.explanation}</p>
      </article>)}</div>}
    </section>
    <section className="rounded-xl border border-amber-200 bg-amber-50/50 p-5">
      <h2 className="font-semibold">Zəif olduğum mövzular</h2>
      {result.weakTopics.length ? <><p className="mt-2 text-sm text-slate-600">Bu mövzuları sənədindən yenidən oxu və ayrıca araşdır.</p><ul className="mt-3 flex flex-wrap gap-2">{result.weakTopics.map(topic => <li key={topic} className="rounded-full border border-amber-200 bg-white px-3 py-1.5 text-sm text-amber-900">{topic}</li>)}</ul></> : <p className="mt-2 text-sm text-slate-600">Bu məşqdə zəif mövzu müəyyən edilmədi. Bütün cavabların doğrudur.</p>}
    </section>
  </div>
}
