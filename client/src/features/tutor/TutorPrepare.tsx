import { BookOpenText, Check, CheckCircle2, ChevronDown, CircleHelp, ClipboardCheck, Copy, Lightbulb, ListChecks, MessageSquareText } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { Alert } from '../../components/common/Alert'
import { cn } from '../../lib/utils'
import { usePrepareMaterial, usePrepareTopics, type GroupAnswer, type PrepareTopic, type PrepareTopics, type PrepareView, type TopicExplanation } from './api'
import { Skeleton } from './TutorShared'

interface Props {
  chatId: string
  disabled: boolean
  view: PrepareView
  onView: (view: PrepareView) => void
  /** Opens the chat with a question about the topic. */
  onAsk: (text: string) => void
  /** "Bu mövzudan məni yoxla": opens Məni yoxla limited to the topic's questions. */
  onQuiz: (topic: PrepareTopic) => void
  /** From Məni yoxla ("Mövzunu izahda aç"): the topic containing this question number opens first. */
  focusNumber?: number
}

const views: { id: PrepareView; label: string; hint: string; icon: typeof BookOpenText }[] = [
  { id: 'explain', label: 'Mövzunu anla', hint: 'Sadə izah, əsas anlayışlar və yadda saxlanılacaq faktlar', icon: BookOpenText },
  { id: 'answer', label: 'İmtahan cavabı', hint: 'Hər mövzu üçün ortaq cavab və hər suala xüsusi əlavə', icon: ClipboardCheck },
]

/** "Hazırlaş": the document's open questions grouped into topics; each topic can be understood or turned into an exam answer. */
export function TutorPrepare({ chatId, disabled, view, onView, onAsk, onQuiz, focusNumber }: Props) {
  const topics = usePrepareTopics(chatId)
  // undefined = the student has not chosen yet: then the topic of focusNumber (if any) is open.
  const [chosenKey, setOpenKey] = useState<string | null | undefined>(undefined)
  const focusKey = topics.data?.topics.find(t => t.questions.some(q => q.number === focusNumber))?.key ?? null
  const openKey = chosenKey === undefined ? focusKey : chosenKey

  return (
    <div className="mx-auto flex w-full max-w-3xl min-w-0 flex-col gap-4 px-4 py-5 sm:py-8" style={{ overflowWrap: 'anywhere' }}>
      <div role="tablist" aria-label="Hazırlaş rejimi" className="tutor-sunken grid grid-cols-2 gap-1 rounded-2xl p-1">
        {views.map(({ id, label, hint, icon: Icon }) => (
          <button key={id} type="button" role="tab" aria-selected={view === id} onClick={() => onView(id)}
            className={cn('flex min-h-14 min-w-0 flex-col items-start justify-center rounded-xl border px-3 py-2 text-left transition-colors sm:px-4',
              view === id ? 'tutor-border bg-white text-slate-900 shadow-sm' : 'border-transparent text-slate-500 hover:text-slate-800')}>
            <span className="flex items-center gap-2 text-sm font-semibold"><Icon className={cn('size-4 shrink-0', view === id && 'text-indigo-700')} aria-hidden />{label}</span>
            <span className="mt-0.5 hidden text-xs text-slate-500 sm:block">{hint}</span>
          </button>
        ))}
      </div>

      {disabled && <Alert tone="info" title="AI köməkçisi hazırda söndürülüb">Əvvəl hazırlanmış mövzular açıla bilər, yeniləri hazırlanmır.</Alert>}
      {/* The counts and the list of unused questions below say the same as the warning, so it is shown only without them. */}
      {topics.data?.warning && !topics.data.stats && <Alert tone="info" title="Qeyd">{topics.data.warning}</Alert>}

      {topics.isLoading ? (
        <Skeleton label="Suallar mövzulara ayrılır…" />
      ) : topics.isError ? (
        <Alert title="Mövzular hazırlanmadı" action={<button type="button" onClick={() => topics.refetch()} className="text-sm font-semibold underline">Yenidən cəhd et</button>}>
          {topics.error.message}
        </Alert>
      ) : (
        <>
          <DocumentSummary data={topics.data!} />
          <ul className="flex flex-col gap-2.5">
            {topics.data!.topics.map((topic, i, all) => {
              const open = openKey === topic.key
              const ready = view === 'explain' ? topic.explained : topic.answered
              // Main topic heading above its first subtopic.
              const newSection = topic.section && topic.section !== all[i - 1]?.section
              return (
                <li key={topic.key} className="contents">
                {newSection && <h3 className="mt-2 px-1 text-xs font-semibold tracking-wide text-slate-500 uppercase first:mt-0">{topic.section}</h3>}
                <div className="tutor-border rounded-2xl border bg-white"
                  ref={open && chosenKey === undefined ? el => el?.scrollIntoView({ block: 'start' }) : undefined}>
                  <button type="button" onClick={() => setOpenKey(open ? null : topic.key)} aria-expanded={open}
                    className="flex min-h-14 w-full items-center gap-3 px-4 py-3 text-left">
                    <span className="min-w-0 flex-1">
                      <span className="block text-[15px] font-semibold leading-snug text-slate-900">{topic.title}</span>
                      <span className="mt-0.5 flex items-center gap-1.5 text-xs text-slate-500">
                        {topic.questions.length} sual
                        {ready && <><span aria-hidden>·</span><CheckCircle2 className="size-3.5 text-emerald-600" aria-hidden />hazırdır</>}
                      </span>
                    </span>
                    <ChevronDown className={cn('size-4 shrink-0 text-slate-500 transition-transform', open && 'rotate-180')} aria-hidden />
                  </button>
                  {open && (
                    <div className="tutor-border tutor-enter border-t px-4 pt-4 pb-4">
                      {view === 'explain'
                        ? <TopicExplain chatId={chatId} topic={topic} onAsk={onAsk} onQuiz={onQuiz} />
                        : <TopicAnswer chatId={chatId} topic={topic} />}
                    </div>
                  )}
                </div>
                </li>
              )
            })}
          </ul>
        </>
      )}
    </div>
  )
}

/** How many questions the document has and why some are not used; unused ones are listed, collapsed. */
function DocumentSummary({ data }: { data: PrepareTopics }) {
  const stats = data.stats
  const questions = data.topics.reduce((sum, t) => sum + t.questions.length, 0)
  if (!stats) return <p className="text-sm text-slate-500">{data.topics.length} mövzu · {questions} açıq sual</p>
  const unused = data.excluded ?? []
  return (
    <div className="text-sm text-slate-500">
      <p>
        {data.topics.length} mövzu · sənəddə {stats.found} sual tapıldı, <b className="font-semibold text-slate-700">{stats.usable}</b> sual öyrənmə və yoxlamada istifadə olunur
        {stats.sharedAnswers > 0 && <> ({stats.sharedAnswers} sual sənəddə qonşu sualla ortaq cavab alır)</>}
      </p>
      {unused.length > 0 && (
        <details className="mt-1.5">
          <summary className="cursor-pointer font-medium text-slate-600 hover:text-slate-900">İstifadə olunmayan suallar ({unused.length})</summary>
          <p className="mt-1.5 text-xs">Bu suallar silinmir, sadəcə etibarlı cavab olmadan izah və yoxlamaya daxil edilmir.</p>
          <ul className="mt-1.5 space-y-1">
            {unused.map(q => (
              <li key={q.number}><span className="font-semibold text-slate-700">{q.number}.</span> {q.text} <span className="text-xs text-amber-700">— {q.reason}</span></li>
            ))}
          </ul>
        </details>
      )}
    </div>
  )
}

function Section({ title, icon: Icon, children }: { title: string; icon: typeof BookOpenText; children: ReactNode }) {
  return (
    <section className="mt-5 first:mt-0">
      <h4 className="flex items-center gap-1.5 text-xs font-semibold tracking-wide text-slate-500 uppercase"><Icon className="size-3.5" aria-hidden />{title}</h4>
      <div className="mt-2">{children}</div>
    </section>
  )
}

function Pages({ pages }: { pages: number[] }) {
  return pages.length > 0 ? <p className="mt-4 text-xs text-slate-500">Mənbə səhifələri: {pages.join(', ')}</p> : null
}

function Failure({ message, onRetry }: { message: string; onRetry: () => void }) {
  return <Alert title="Hazırlana bilmədi" action={<button type="button" onClick={onRetry} className="text-sm font-semibold underline">Yenidən cəhd et</button>}>{message}</Alert>
}

function TopicExplain({ chatId, topic, onAsk, onQuiz }: { chatId: string; topic: PrepareTopic; onAsk: Props['onAsk']; onQuiz: Props['onQuiz'] }) {
  const material = usePrepareMaterial<TopicExplanation>(chatId, 'explain', topic.key)
  if (material.isLoading) return <Skeleton label="Mövzu izahı hazırlanır…" />
  if (material.isError) return <Failure message={material.error.message} onRetry={() => material.refetch()} />
  const data = material.data!.payload

  return (
    <div aria-live="polite">
      <Section title="Sadə izah" icon={Lightbulb}>
        <p className="text-[15px] leading-[1.8] text-slate-800">{data.explanation.join(' ')}</p>
      </Section>
      {data.concepts.length > 0 && (
        <Section title="Əsas anlayışlar" icon={BookOpenText}>
          <dl className="grid gap-2 sm:grid-cols-2">
            {data.concepts.map(c => (
              <div key={c.term} className="tutor-sunken rounded-xl px-3 py-2.5">
                <dt className="text-sm font-semibold text-slate-900">{c.term}</dt>
                <dd className="mt-0.5 text-sm leading-relaxed text-slate-600">{c.meaning}</dd>
              </div>
            ))}
          </dl>
        </Section>
      )}
      {data.example && (
        <Section title="Sənəddən nümunə" icon={MessageSquareText}>
          <p className="rounded-xl bg-indigo-50 px-3.5 py-2.5 text-sm leading-relaxed text-indigo-900">{data.example}</p>
        </Section>
      )}
      {data.facts.length > 0 && (
        <Section title="Yadda saxla" icon={ListChecks}>
          <ul className="space-y-1.5">
            {data.facts.map(fact => (
              <li key={fact} className="flex gap-2 text-sm leading-relaxed text-slate-700"><Check className="mt-0.5 size-4 shrink-0 text-emerald-600" aria-hidden />{fact}</li>
            ))}
          </ul>
        </Section>
      )}
      <Pages pages={data.sourcePages} />
      <QuestionList topic={topic} />
      <div className="mt-4 grid grid-cols-1 gap-2 sm:grid-cols-2">
        <button type="button" onClick={() => onQuiz(topic)} className="btn-primary inline-flex min-h-11 items-center justify-center gap-2 rounded-xl px-4 text-sm font-semibold">
          <CircleHelp className="size-4" aria-hidden />Bu mövzudan məni yoxla
        </button>
        <button type="button" onClick={() => onAsk(`"${topic.title}" mövzusunu sənədə əsasən daha sadə izah et.`)}
          className="tutor-shared-action"><MessageSquareText className="size-4" aria-hidden />Tutor-dan soruş</button>
      </div>
    </div>
  )
}

function QuestionList({ topic }: { topic: PrepareTopic }) {
  return (
    <details className="group mt-3">
      <summary className="cursor-pointer text-sm font-medium text-slate-600 hover:text-slate-900">Bu mövzunun sualları ({topic.questions.length})</summary>
      <ol className="mt-2 space-y-1 text-sm text-slate-600">
        {topic.questions.map(q => <li key={q.number}><span className="font-semibold text-slate-800">{q.number}.</span> {q.text}{q.page ? <span className="text-slate-400"> · səh. {q.page}</span> : null}</li>)}
      </ol>
    </details>
  )
}

function TopicAnswer({ chatId, topic }: { chatId: string; topic: PrepareTopic }) {
  const material = usePrepareMaterial<GroupAnswer>(chatId, 'answer', topic.key)
  const [copied, setCopied] = useState<'ok' | 'error' | null>(null)
  if (material.isLoading) return <Skeleton label="Ortaq imtahan cavabı hazırlanır…" />
  if (material.isError) return <Failure message={material.error.message} onRetry={() => material.refetch()} />
  const data = material.data!.payload
  const covered = data.questions.filter(q => q.covered)
  const uncovered = data.questions.filter(q => !q.covered)

  const copy = async () => {
    const text = [data.title, data.answer, ...covered.map(q => `${q.number}. ${q.text}\n${q.addition}`)].join('\n\n')
    try { await navigator.clipboard.writeText(text); setCopied('ok'); setTimeout(() => setCopied(null), 1500) } catch { setCopied('error') }
  }

  return (
    <div aria-live="polite">
      {data.sufficient ? (
        <>
          <Section title="Ortaq imtahan cavabı" icon={ClipboardCheck}>
            <p className="text-[15px] leading-[1.85] text-slate-800">{data.answer}</p>
          </Section>
          <Section title={`Əhatə olunan suallar (${covered.length}/${data.questions.length})`} icon={CheckCircle2}>
            <ul className="space-y-2.5">
              {covered.map(q => (
                <li key={q.number} className="tutor-sunken rounded-xl px-3.5 py-2.5">
                  <p className="text-sm font-semibold text-slate-900">{q.number}. {q.text}</p>
                  <p className="mt-1 text-sm leading-relaxed text-slate-700"><span className="font-semibold text-indigo-700">+ </span>{q.addition}</p>
                </li>
              ))}
            </ul>
          </Section>
          <Pages pages={data.sourcePages} />
          <button type="button" onClick={copy} className="tutor-shared-action mt-4 w-full sm:w-auto">
            {copied === 'ok' ? <Check className="size-4" aria-hidden /> : <Copy className="size-4" aria-hidden />}{copied === 'ok' ? 'Kopyalandı' : 'Cavabı kopyala'}
          </button>
          {copied === 'error' && <p role="alert" className="mt-2 text-sm text-rose-700">Brauzer kopyalamağa icazə vermədi. Mətni seçib kopyalaya bilərsiniz.</p>}
        </>
      ) : (
        <Alert tone="info" title="Ortaq cavab qurulmadı">Bu suallar üçün sənəddə ortaq cavab yazmağa kifayət qədər ümumi məlumat yoxdur. Hər sualın sənəddəki cavabı aşağıdadır.</Alert>
      )}
      {uncovered.length > 0 && (
        <Section title={data.sufficient ? 'Ortaq cavaba uyğun gəlməyən suallar' : 'Sənəddəki cavablar'} icon={CircleHelp}>
          <ul className="space-y-2">
            {uncovered.map(q => (
              <li key={q.number}>
                <details className="tutor-border rounded-xl border px-3.5 py-2.5">
                  <summary className="cursor-pointer text-sm font-semibold text-slate-800">{q.number}. {q.text}</summary>
                  <p className="mt-2 text-sm leading-relaxed whitespace-pre-line text-slate-600">{q.documentAnswer}</p>
                </details>
              </li>
            ))}
          </ul>
        </Section>
      )}
    </div>
  )
}
