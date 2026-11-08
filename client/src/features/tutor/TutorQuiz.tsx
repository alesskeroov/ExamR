import { ArrowRight, BookOpenText, Check, CircleHelp, Flag, History, Loader2, RotateCcw, RotateCw, TriangleAlert, X } from 'lucide-react'
import { useEffect, useRef, useState, type KeyboardEvent } from 'react'
import { Alert } from '../../components/common/Alert'
import { ApiError } from '../../lib/api-client'
import { cn } from '../../lib/utils'
import { useAnswerQuestion, useNextQuestion, useQuizReview, type QuizQuestion, type QuizResult, type QuizReview, type QuizStatus } from './api'

const statusView: Record<QuizStatus, { label: string; tone: string; icon: typeof Check }> = {
  Correct: { label: 'Düzgün', tone: 'border-emerald-200 bg-emerald-50 text-emerald-800', icon: Check },
  Incorrect: { label: 'Səhv', tone: 'border-rose-200 bg-rose-50 text-rose-800', icon: X },
  Unchecked: { label: 'Yoxlanılmadı', tone: 'border-slate-200 bg-slate-50 text-slate-700', icon: TriangleAlert },
}

/** Questions per session; a topic or review session is shorter when it has fewer questions. */
const sessionSize = 10

type Scope = { kind: 'all' } | { kind: 'topic'; title: string; numbers: number[] } | { kind: 'review'; numbers: number[] }
/** numberOf: quiz item id → question number, so a wrong answer can link back to its topic. */
interface Session { scope: Scope; size: number; asked: number[]; results: QuizResult[]; numberOf: Record<string, number> }

interface QuizProps {
  chatId: string
  disabled: boolean
  /** Set from Hazırlaş ("Bu mövzudan məni yoxla"): offers a session on this topic's questions. */
  focus?: { title: string; numbers: number[] }
  /** Opens the Hazırlaş explanation of the topic that contains this question number. */
  onExplain?: (number: number) => void
}

/** "Məni yoxla": a focused session of one question at a time from the PDF's open questions, graded by meaning. */
export function TutorQuiz({ chatId, disabled, focus, onExplain }: QuizProps) {
  const next = useNextQuestion(chatId)
  const answer = useAnswerQuestion(chatId)
  const review = useQuizReview(chatId)
  const [session, setSession] = useState<Session | null>(null)
  const [question, setQuestion] = useState<QuizQuestion | null>(null)
  const [result, setResult] = useState<QuizResult | null>(null)
  const [finished, setFinished] = useState(false)
  const [text, setText] = useState('')
  const area = useRef<HTMLTextAreaElement>(null)

  const fetchNext = (current: Session) => {
    setResult(null); setText(''); answer.reset()
    next.mutate({ numbers: current.scope.kind === 'all' ? [] : current.scope.numbers, asked: current.asked }, {
      onSuccess: q => {
        setQuestion(q)
        setSession({ ...current, asked: [...current.asked, q.number], numberOf: { ...current.numberOf, [q.id]: q.number } })
        requestAnimationFrame(() => area.current?.focus())
      },
      // Every question of the scope was asked: the session is over.
      onError: error => { if (error instanceof ApiError && error.status === 409 && current.asked.length > 0) finish() },
    })
  }
  /** `first`: an unanswered question from before ("Qaldığın yerdən davam et") opens the session instead of a new one. */
  const start = (scope: Scope, first?: QuizQuestion) => {
    const size = scope.kind === 'all' ? sessionSize : Math.min(sessionSize, scope.numbers.length)
    const fresh: Session = { scope, size, asked: [], results: [], numberOf: {} }
    setSession(fresh); setFinished(false); setQuestion(null)
    if (!first) { fetchNext(fresh); return }
    setResult(null); setText(''); answer.reset(); next.reset()
    setQuestion(first)
    setSession({ ...fresh, asked: [first.number], numberOf: { [first.id]: first.number } })
    requestAnimationFrame(() => area.current?.focus())
  }
  const finish = () => { setQuestion(null); setResult(null); setFinished(true); next.reset() }
  const check = (giveUp: boolean) => {
    if (!question || answer.isPending) return
    answer.mutate({ id: question.id, answer: giveUp ? undefined : text, giveUp }, {
      onSuccess: graded => {
        setResult(graded)
        setSession(s => s && { ...s, results: [...s.results.filter(r => r.id !== graded.id), graded] })
      },
    })
  }
  const advance = () => session && (session.asked.length >= session.size ? finish() : fetchNext(session))
  const onKey = (event: KeyboardEvent<HTMLTextAreaElement>) => {
    if (event.key === 'Enter' && (event.ctrlKey || event.metaKey)) { event.preventDefault(); check(false) }
  }
  useEffect(() => { if (result) document.getElementById('quiz-next')?.focus() }, [result])

  const active = session && !finished
  const isLast = !!session && session.asked.length >= session.size

  return (
    <div className="mx-auto flex w-full max-w-2xl min-w-0 flex-col gap-4 px-4 py-5 sm:py-8" style={{ overflowWrap: 'anywhere' }}>
      {!active && !finished && (
        <StartScreen review={review} focus={focus} disabled={disabled} onStart={start} onExplain={onExplain} />
      )}

      {finished && session && (
        <Summary session={session} review={review.data} onRestart={() => { setSession(null); setFinished(false) }} onStart={start} onExplain={onExplain} />
      )}

      {active && (
        <>
          <div className="flex items-center gap-3">
            <div className="min-w-0 flex-1">
              <p className="truncate text-xs font-medium text-slate-500">
                {session.scope.kind === 'topic' ? `Mövzu: ${session.scope.title}` : session.scope.kind === 'review' ? 'Səhvlərin təkrarı' : 'Bütün sənəd'}
                {' · '}Sual {Math.max(1, session.asked.length)}/{session.size}
              </p>
              <div className="mt-1.5 h-1.5 overflow-hidden rounded-full bg-slate-100" role="progressbar" aria-label="Sessiya irəliləyişi"
                aria-valuemin={0} aria-valuemax={session.size} aria-valuenow={session.results.length}>
                <div className="h-full rounded-full bg-indigo-500 transition-[width] duration-300" style={{ width: `${(session.results.length / session.size) * 100}%` }} />
              </div>
            </div>
            <button type="button" onClick={finish} className="inline-flex h-9 shrink-0 items-center gap-1.5 rounded-lg px-2.5 text-sm font-medium text-slate-600 hover:bg-slate-100">
              <Flag className="size-4" aria-hidden />Bitir
            </button>
          </div>

          {next.isPending && (
            <div className="tutor-border space-y-3 rounded-2xl border bg-white p-5" role="status" aria-label="Sual hazırlanır">
              <div className="h-3 w-24 animate-pulse rounded bg-slate-100" />
              <div className="h-5 w-full animate-pulse rounded bg-slate-100" />
              <div className="h-5 w-2/3 animate-pulse rounded bg-slate-100" />
            </div>
          )}
          {next.isError && (
            <Alert title="Sual açılmadı" action={<button type="button" onClick={() => fetchNext(session)} className="text-sm font-semibold underline">Yenidən cəhd et</button>}>
              {next.error.message}
            </Alert>
          )}

          {question && !next.isPending && (
            <section className="tutor-enter tutor-border rounded-2xl border bg-white p-4 sm:p-6" aria-labelledby="quiz-question">
              <p className="text-xs font-medium tracking-wide text-slate-500 uppercase">Sual {question.number}{question.pageFrom ? ` · səh. ${question.pageFrom}` : ''}</p>
              <h2 id="quiz-question" className="mt-1.5 text-[17px] leading-7 font-semibold text-slate-900">{question.question}</h2>

              {!result ? (
                <>
                  <label htmlFor="quiz-answer" className="sr-only">Cavabınız</label>
                  <textarea id="quiz-answer" ref={area} value={text} onChange={e => setText(e.target.value)} onKeyDown={onKey} rows={5} maxLength={4000}
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
                <ResultCard result={result} last={isLast} retrying={answer.isPending}
                  onExplain={onExplain && result.status !== 'Correct' ? () => onExplain(question.number) : undefined}
                  onRetry={() => answer.mutate({ id: result.id }, { onSuccess: graded => { setResult(graded); setSession(s => s && { ...s, results: [...s.results.filter(r => r.id !== graded.id), graded] }) } })}
                  onNext={advance} />
              )}
            </section>
          )}
        </>
      )}
    </div>
  )
}

function StatusBadge({ status }: { status: QuizStatus }) {
  const { label, tone, icon: Icon } = statusView[status]
  return <span className={cn('inline-flex shrink-0 items-center gap-1 rounded-full border px-2 py-0.5 text-xs font-semibold', tone)}><Icon className="size-3.5" aria-hidden />{label}</span>
}

function StartScreen({ review, focus, disabled, onStart, onExplain }: {
  review: ReturnType<typeof useQuizReview>; focus: QuizProps['focus']; disabled: boolean; onStart: (scope: Scope, first?: QuizQuestion) => void
  onExplain?: (number: number) => void
}) {
  const data = review.data
  const answered = (data?.correct ?? 0) + (data?.incorrect ?? 0)
  return (
    <>
      <div className="tutor-enter tutor-border rounded-2xl border bg-white px-5 py-7 text-center sm:px-8">
        <span className="mx-auto grid size-11 place-items-center rounded-xl bg-indigo-50 text-indigo-700" aria-hidden><CircleHelp className="size-5" /></span>
        <h2 className="mt-3 text-lg font-semibold tracking-tight">Biliyini yoxla</h2>
        <p className="mx-auto mt-1 max-w-sm text-sm text-slate-600">PDF-dəki açıq suallardan biri veriləcək. Öz sözlərinlə cavab yaz — cavab mənaca yoxlanılır.</p>
        {data && answered > 0 && (
          <p className="mt-3 text-xs text-slate-500" aria-label="Ümumi nəticə">
            <b className="text-emerald-700">{data.correct}</b> düzgün · <b className="text-rose-700">{data.incorrect}</b> səhv
            {data.unchecked > 0 && <> · <b className="text-slate-700">{data.unchecked}</b> yoxlanılmadı</>} · {data.questions} sualdan
          </p>
        )}
        <div className="mx-auto mt-5 flex max-w-sm flex-col gap-2">
          {focus ? (
            <>
              <button type="button" onClick={() => onStart({ kind: 'topic', title: focus.title, numbers: focus.numbers })} disabled={disabled}
                className="btn-primary inline-flex min-h-11 items-center justify-center gap-2 rounded-xl px-5 text-sm font-semibold disabled:opacity-50">
                Mövzudan başla: {focus.title || 'seçilmiş mövzu'} <ArrowRight className="size-4 shrink-0" aria-hidden />
              </button>
              <button type="button" onClick={() => onStart({ kind: 'all' })} disabled={disabled}
                className="tutor-shared-action">Bütün sənəddən başla</button>
            </>
          ) : (
            <button type="button" onClick={() => onStart({ kind: 'all' })} disabled={disabled}
              className="btn-primary inline-flex min-h-11 items-center justify-center gap-2 rounded-xl px-5 text-sm font-semibold disabled:opacity-50">
              Bütün sənəddən başla <ArrowRight className="size-4" aria-hidden />
            </button>
          )}
        </div>
        {disabled && <p className="mt-3 text-sm text-slate-500">AI köməkçisi hazırda aktiv deyil.</p>}
      </div>

      {data?.pending && !focus && (
        <section className="tutor-border flex flex-wrap items-center gap-3 rounded-2xl border bg-white p-4" aria-label="Yarımçıq sual">
          <History className="size-4 shrink-0 text-slate-500" aria-hidden />
          <p className="min-w-0 flex-1 text-sm"><span className="font-semibold">Qaldığın yerdən davam et</span>
            <span className="block truncate text-slate-500">{data.pending.number}. {data.pending.question}</span></p>
          <button type="button" onClick={() => onStart({ kind: 'all' }, data.pending!)} disabled={disabled}
            className="tutor-shared-action shrink-0">Davam et</button>
        </section>
      )}
      {review.isError && <Alert title="Təkrar siyahısı yüklənmədi">{review.error.message}</Alert>}
      {data && <ReviewPanel review={data} disabled={disabled} onStart={onStart} onExplain={onExplain} />}
    </>
  )
}

/** Compact review area under the start card: never part of the question screen. */
function ReviewPanel({ review, disabled, onStart, onExplain }: { review: QuizReview; disabled: boolean; onStart: (scope: Scope) => void; onExplain?: (number: number) => void }) {
  if (review.review.length === 0 && review.weakTopics.length === 0) return null
  return (
    <section className="tutor-border rounded-2xl border bg-white p-4" aria-label="Təkrar">
      {review.review.length > 0 && (
        <div className="flex flex-wrap items-center gap-3">
          <p className="flex min-w-0 flex-1 items-center gap-2 text-sm font-semibold"><RotateCcw className="size-4 shrink-0 text-slate-500" aria-hidden />Təkrar üçün {review.review.length} sual</p>
          <button type="button" onClick={() => onStart({ kind: 'review', numbers: review.review.map(r => r.number) })} disabled={disabled}
            className="btn-primary inline-flex h-10 w-full items-center justify-center rounded-xl px-4 text-sm font-semibold disabled:opacity-50 sm:w-auto">Səhv etdiklərimi təkrar et</button>
        </div>
      )}
      {review.weakTopics.length > 0 && <WeakTopics topics={review.weakTopics} disabled={disabled} onStart={onStart} />}
      {review.review.length > 0 && (
        <details className="mt-3">
          <summary className="cursor-pointer text-sm font-medium text-slate-600 hover:text-slate-900">Siyahını göstər</summary>
          <ul className="tutor-border mt-2 divide-y border-t" style={{ borderColor: 'var(--app-border)' }}>
            {review.review.map(item => (
              <li key={item.number} className="py-2.5">
                <p className="text-sm font-medium text-slate-800">{item.number}. {item.question}</p>
                <p className="mt-1 line-clamp-3 text-sm leading-relaxed text-slate-600">{item.correctAnswer}</p>
                {onExplain && <ExplainLink onClick={() => onExplain(item.number)} />}
              </li>
            ))}
          </ul>
        </details>
      )}
    </section>
  )
}

function WeakTopics({ topics, disabled, onStart }: { topics: QuizReview['weakTopics']; disabled: boolean; onStart: (scope: Scope) => void }) {
  return (
    <div className="mt-4 first:mt-0">
      <p className="text-xs font-semibold tracking-wide text-slate-500 uppercase">Zəif mövzular</p>
      <ul className="mt-2 space-y-1.5">
        {topics.map(topic => (
          <li key={topic.title} className="flex items-center gap-2">
            <span className="min-w-0 flex-1 text-sm text-slate-800">{topic.title} <span className="text-xs text-slate-500">· {topic.wrong}/{topic.answered} səhv</span></span>
            <button type="button" onClick={() => onStart({ kind: 'topic', title: topic.title, numbers: topic.numbers })} disabled={disabled}
              className="shrink-0 rounded-lg px-2.5 py-1.5 text-sm font-semibold text-indigo-700 hover:bg-indigo-50 disabled:opacity-50">Yoxla</button>
          </li>
        ))}
      </ul>
    </div>
  )
}

function Summary({ session, review, onRestart, onStart, onExplain }: {
  session: Session; review: QuizReview | undefined; onRestart: () => void; onStart: (scope: Scope) => void; onExplain?: (number: number) => void
}) {
  const count = (status: QuizStatus) => session.results.filter(r => r.status === status).length
  const wrong = session.results.filter(r => r.status === 'Incorrect')
  const tiles: { status: QuizStatus; tone: string }[] = [
    { status: 'Correct', tone: 'bg-emerald-50 text-emerald-800' },
    { status: 'Incorrect', tone: 'bg-rose-50 text-rose-800' },
    { status: 'Unchecked', tone: 'bg-slate-100 text-slate-700' },
  ]
  return (
    <section className="tutor-enter tutor-border rounded-2xl border bg-white p-5 sm:p-6" aria-labelledby="quiz-summary">
      <h2 id="quiz-summary" className="text-lg font-semibold tracking-tight">Sessiya bitdi</h2>
      {session.results.length === 0 ? (
        <p className="mt-1 text-sm text-slate-600">Bu sessiyada heç bir suala cavab verilmədi.</p>
      ) : (
        <div className="mt-4 grid grid-cols-3 gap-2">
          {tiles.map(({ status, tone }) => (
            <div key={status} className={cn('rounded-xl px-3 py-3 text-center', tone)}>
              <p className="text-2xl font-bold">{count(status)}</p>
              <p className="text-xs font-medium">{statusView[status].label}</p>
            </div>
          ))}
        </div>
      )}
      {wrong.length > 0 && (
        <details className="mt-4">
          <summary className="cursor-pointer text-sm font-medium text-slate-600 hover:text-slate-900">Bu sessiyada səhv olanlar ({wrong.length})</summary>
          <ul className="mt-2 space-y-2">
            {wrong.map(r => (
              <li key={r.id} className="tutor-sunken rounded-xl px-3.5 py-2.5">
                <p className="text-sm font-medium text-slate-800">{r.question}</p>
                <p className="mt-1 line-clamp-4 text-sm leading-relaxed text-slate-600">{r.correctAnswer}</p>
                {onExplain && session.numberOf[r.id] && <ExplainLink onClick={() => onExplain(session.numberOf[r.id])} />}
              </li>
            ))}
          </ul>
        </details>
      )}
      {review && review.weakTopics.length > 0 && <div className="mt-4"><WeakTopics topics={review.weakTopics} disabled={false} onStart={onStart} /></div>}
      <div className="mt-5 grid grid-cols-1 gap-2 sm:grid-cols-2">
        <button type="button" onClick={onRestart} className="btn-primary inline-flex min-h-11 items-center justify-center gap-2 rounded-xl px-5 text-sm font-semibold">
          <RotateCw className="size-4" aria-hidden />Yeni sessiya
        </button>
        {review && review.review.length > 0 && (
          <button type="button" onClick={() => onStart({ kind: 'review', numbers: review.review.map(r => r.number) })} className="tutor-shared-action">
            <RotateCcw className="size-4" aria-hidden />Səhv etdiklərimi təkrar et ({review.review.length})
          </button>
        )}
      </div>
    </section>
  )
}

function ExplainLink({ onClick }: { onClick: () => void }) {
  return (
    <button type="button" onClick={onClick} className="mt-1.5 inline-flex min-h-8 items-center gap-1.5 text-sm font-semibold text-indigo-700 hover:text-indigo-800">
      <BookOpenText className="size-4" aria-hidden />Mövzunu izahda aç
    </button>
  )
}

function ResultCard({ result, last, retrying, onRetry, onNext, onExplain }: {
  result: QuizResult; last: boolean; retrying: boolean; onRetry: () => void; onNext: () => void; onExplain?: () => void
}) {
  return (
    <div className="tutor-enter mt-4 space-y-3" aria-live="polite">
      <div className="flex flex-wrap items-center gap-2">
        <StatusBadge status={result.status} />
        <p className="text-sm text-slate-700">{result.feedback}</p>
      </div>
      {result.missingPoints.length > 0 && result.status === 'Incorrect' && (
        <div className="rounded-xl bg-amber-50 px-3.5 py-2.5 text-sm text-amber-900">
          <p className="font-semibold">Çatışmayan əsas fikirlər</p>
          <ul className="mt-1 list-disc space-y-0.5 pl-5">{result.missingPoints.map(point => <li key={point}>{point}</li>)}</ul>
        </div>
      )}
      <div className="tutor-sunken rounded-xl px-3.5 py-3">
        <p className="text-xs font-semibold tracking-wide text-slate-500 uppercase">Sənəddəki cavab{result.pageFrom ? ` · səh. ${result.pageFrom}` : ''}</p>
        <p className="mt-1 text-[15px] leading-relaxed whitespace-pre-line text-slate-800">{result.correctAnswer}</p>
        {onExplain && <ExplainLink onClick={onExplain} />}
      </div>
      <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
        {result.canRetry && (
          <button type="button" onClick={onRetry} disabled={retrying} className="tutor-shared-action">
            {retrying ? <Loader2 className="size-4 animate-spin" aria-hidden /> : <RotateCw className="size-4" aria-hidden />}Yenidən yoxla
          </button>
        )}
        <button id="quiz-next" type="button" onClick={onNext} disabled={retrying} className="btn-primary inline-flex h-11 items-center justify-center gap-2 rounded-xl px-5 text-sm font-semibold disabled:opacity-50">
          {last ? 'Nəticəni gör' : 'Növbəti sual'} <ArrowRight className="size-4" aria-hidden />
        </button>
      </div>
    </div>
  )
}
