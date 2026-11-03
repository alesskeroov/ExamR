import { ArrowRight, Check, ChevronDown, CircleHelp, Loader2, RotateCcw, X } from 'lucide-react'
import { useEffect, useRef, useState, type KeyboardEvent } from 'react'
import { Alert } from '../../components/common/Alert'
import { cn } from '../../lib/utils'
import { useAnswerQuestion, useNextQuestion, useQuizReview, type QuizQuestion, type QuizResult, type QuizStatus } from './api'

const statusView: Record<QuizStatus, { label: string; tone: string; icon: typeof Check }> = {
  Correct: { label: 'Düzgün', tone: 'border-emerald-200 bg-emerald-50 text-emerald-800', icon: Check },
  Partial: { label: 'Natamam', tone: 'border-amber-200 bg-amber-50 text-amber-800', icon: CircleHelp },
  Incorrect: { label: 'Səhv', tone: 'border-rose-200 bg-rose-50 text-rose-800', icon: X },
}

/** "Məni yoxla": one document question at a time, graded Correct / Partial / Incorrect with the short right answer. */
export function TutorQuiz({ chatId, disabled }: { chatId: string; disabled: boolean }) {
  const next = useNextQuestion(chatId)
  const answer = useAnswerQuestion(chatId)
  const review = useQuizReview(chatId)
  const [question, setQuestion] = useState<QuizQuestion | null>(null)
  const [result, setResult] = useState<QuizResult | null>(null)
  const [text, setText] = useState('')
  const [reviewOpen, setReviewOpen] = useState(false)
  const area = useRef<HTMLTextAreaElement>(null)

  const ask = () => {
    setResult(null); setText(''); answer.reset()
    next.mutate(undefined, { onSuccess: q => { setQuestion(q); requestAnimationFrame(() => area.current?.focus()) } })
  }
  const check = (giveUp: boolean) => {
    if (!question || answer.isPending) return
    answer.mutate({ id: question.id, answer: giveUp ? undefined : text, giveUp }, { onSuccess: setResult })
  }
  const onKey = (event: KeyboardEvent<HTMLTextAreaElement>) => {
    if (event.key === 'Enter' && (event.ctrlKey || event.metaKey)) { event.preventDefault(); check(false) }
  }
  useEffect(() => { if (result) document.getElementById('quiz-next')?.focus() }, [result])

  const stats = review.data
  const total = (stats?.correct ?? 0) + (stats?.partial ?? 0) + (stats?.incorrect ?? 0)

  return (
    <div className="mx-auto flex w-full max-w-2xl flex-col gap-4 px-4 py-5 sm:py-8">
      {total > 0 && (
        <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-slate-500" aria-label="Nəticələr">
          <span><b className="text-emerald-700">{stats!.correct}</b> düzgün</span>
          <span><b className="text-amber-700">{stats!.partial}</b> natamam</span>
          <span><b className="text-rose-700">{stats!.incorrect}</b> səhv</span>
        </div>
      )}

      {!question && !next.isPending && (
        <div className="tutor-enter tutor-border rounded-2xl border bg-white px-5 py-8 text-center sm:px-8">
          <span className="mx-auto grid size-11 place-items-center rounded-xl bg-indigo-50 text-indigo-700" aria-hidden><CircleHelp className="size-5" /></span>
          <h2 className="mt-3 text-lg font-semibold tracking-tight">Biliyini yoxla</h2>
          <p className="mx-auto mt-1 max-w-sm text-sm text-slate-600">Sənəddən qısa sual veriləcək. Cavabını yaz və ya "Bilmirəm" seç — düzgün cavabı dərhal görəcəksən.</p>
          <button type="button" onClick={ask} disabled={disabled}
            className="btn-primary mt-5 inline-flex h-11 items-center gap-2 rounded-xl px-5 text-sm font-semibold disabled:opacity-50">
            İlk sualı ver <ArrowRight className="size-4" aria-hidden />
          </button>
        </div>
      )}

      {next.isPending && (
        <div className="tutor-border space-y-3 rounded-2xl border bg-white p-5" role="status" aria-label="Sual hazırlanır">
          <div className="h-3 w-24 animate-pulse rounded bg-slate-100" />
          <div className="h-5 w-full animate-pulse rounded bg-slate-100" />
          <div className="h-5 w-2/3 animate-pulse rounded bg-slate-100" />
        </div>
      )}
      {next.isError && <Alert title="Sual hazırlanmadı" action={<button type="button" onClick={ask} className="text-sm font-semibold underline">Yenidən</button>}>{next.error.message}</Alert>}

      {question && !next.isPending && (
        <section className="tutor-enter tutor-border rounded-2xl border bg-white p-4 sm:p-6" aria-labelledby="quiz-question">
          <p className="text-xs font-medium tracking-wide text-slate-500 uppercase">
            Sual{question.pageFrom ? ` · səh. ${question.pageFrom}${question.pageTo && question.pageTo !== question.pageFrom ? `–${question.pageTo}` : ''}` : ''}
          </p>
          <h2 id="quiz-question" className="mt-1.5 text-[17px] leading-7 font-semibold text-slate-900">{question.question}</h2>

          {!result ? (
            <>
              <label htmlFor="quiz-answer" className="sr-only">Cavabınız</label>
              <textarea id="quiz-answer" ref={area} value={text} onChange={e => setText(e.target.value)} onKeyDown={onKey} rows={4} maxLength={4000}
                disabled={answer.isPending} placeholder="Cavabınızı öz sözlərinizlə yazın…"
                className="tutor-border mt-4 block w-full resize-y rounded-xl border bg-white px-3.5 py-3 text-[15px] leading-relaxed outline-none placeholder:text-slate-400 focus:border-indigo-400" />
              {answer.isError && <p role="alert" className="mt-2 text-sm text-rose-700">{answer.error.message}</p>}
              <div className="mt-3 flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
                <button type="button" onClick={() => check(true)} disabled={answer.isPending}
                  className="tutor-border inline-flex h-11 items-center justify-center rounded-xl border px-4 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-50">
                  Bilmirəm
                </button>
                <button type="button" onClick={() => check(false)} disabled={answer.isPending || !text.trim()}
                  className="btn-primary inline-flex h-11 items-center justify-center gap-2 rounded-xl px-5 text-sm font-semibold disabled:opacity-50">
                  {answer.isPending && <Loader2 className="size-4 animate-spin" aria-hidden />}
                  {answer.isPending ? 'Yoxlanılır…' : 'Cavabı yoxla'}
                </button>
              </div>
              <p className="mt-2 hidden text-right text-[11px] text-slate-400 sm:block">Ctrl+Enter — yoxla</p>
            </>
          ) : (
            <ResultCard result={result} onNext={ask} />
          )}
        </section>
      )}

      {stats && stats.items.length > 0 && (
        <section className="tutor-border rounded-2xl border bg-white">
          <button type="button" onClick={() => setReviewOpen(v => !v)} aria-expanded={reviewOpen}
            className="flex min-h-12 w-full items-center justify-between gap-2 px-4 text-left text-sm font-semibold">
            <span className="flex items-center gap-2"><RotateCcw className="size-4 text-slate-500" aria-hidden />Təkrar üçün ({stats.items.length})</span>
            <ChevronDown className={cn('size-4 text-slate-500 transition-transform', reviewOpen && 'rotate-180')} aria-hidden />
          </button>
          {reviewOpen && (
            <ul className="tutor-border divide-y border-t" style={{ borderColor: 'var(--app-border)' }}>
              {stats.items.map(item => (
                <li key={item.id} className="px-4 py-3">
                  <div className="flex items-start gap-2">
                    <StatusBadge status={item.status} />
                    <p className="min-w-0 flex-1 text-sm font-medium text-slate-800">{item.question}</p>
                  </div>
                  <p className="mt-1.5 text-sm leading-relaxed text-slate-600">{item.correctAnswer}</p>
                </li>
              ))}
            </ul>
          )}
        </section>
      )}
    </div>
  )
}

function StatusBadge({ status }: { status: QuizStatus }) {
  const { label, tone, icon: Icon } = statusView[status]
  return <span className={cn('inline-flex shrink-0 items-center gap-1 rounded-full border px-2 py-0.5 text-xs font-semibold', tone)}><Icon className="size-3.5" aria-hidden />{label}</span>
}

function ResultCard({ result, onNext }: { result: QuizResult; onNext: () => void }) {
  return (
    <div className="tutor-enter mt-4 space-y-3" aria-live="polite">
      <div className="flex flex-wrap items-center gap-2">
        <StatusBadge status={result.status} />
        <p className="text-sm text-slate-700">{result.feedback}</p>
      </div>
      {result.missingPoints.length > 0 && result.status !== 'Correct' && (
        <div className="rounded-xl bg-amber-50 px-3.5 py-2.5 text-sm text-amber-900">
          <p className="font-semibold">Çatışmayan əsas məqamlar</p>
          <ul className="mt-1 list-disc space-y-0.5 pl-5">{result.missingPoints.map(point => <li key={point}>{point}</li>)}</ul>
        </div>
      )}
      <div className="tutor-sunken rounded-xl px-3.5 py-3">
        <p className="text-xs font-semibold tracking-wide text-slate-500 uppercase">Qısa düzgün cavab</p>
        <p className="mt-1 text-[15px] leading-relaxed text-slate-800">{result.correctAnswer}</p>
      </div>
      <div className="flex justify-end">
        <button id="quiz-next" type="button" onClick={onNext} className="btn-primary inline-flex h-11 items-center gap-2 rounded-xl px-5 text-sm font-semibold">
          Növbəti sual <ArrowRight className="size-4" aria-hidden />
        </button>
      </div>
    </div>
  )
}
