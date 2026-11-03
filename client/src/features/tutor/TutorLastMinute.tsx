import { Bookmark, BookmarkCheck, Check, Copy, Timer } from 'lucide-react'
import { useMemo, useState } from 'react'
import { Alert } from '../../components/common/Alert'
import { cn } from '../../lib/utils'
import { useLastMinute, useMaterial, useMaterials, useSaveMaterial, type LastMinuteSheet } from './api'
import { Skeleton } from './TutorShared'

const durations = [15, 30, 60] as const

/** "Son dəqiqə hazırlığı": a topic summary sized to the time left, ordered by how often the document's questions ask about each topic. */
export function TutorLastMinute({ chatId, disabled }: { chatId: string; disabled: boolean }) {
  const generate = useLastMinute(chatId)
  const materials = useMaterials(chatId)
  const save = useSaveMaterial(chatId)
  const [minutes, setMinutes] = useState<number>(30)
  const [chosenId, setChosenId] = useState<string | null>(null)
  const previous = useMemo(() => (materials.data ?? []).filter(m => m.kind === 'LastMinute'), [materials.data])
  // Show the newest sheet of this document until the student picks or generates another one.
  const materialId = chosenId ?? previous[0]?.id ?? null
  const material = useMaterial<LastMinuteSheet>(materialId)
  const [copied, setCopied] = useState(false)

  const run = () => generate.mutate({ minutes }, { onSuccess: m => setChosenId(m.id) })
  const sheet = material.data?.payload

  const copy = async () => {
    if (!sheet) return
    const text = [
      ...sheet.topics.flatMap(t => [t.title, t.explanation, ...(t.remember.length ? ['Yadda saxla:', ...t.remember.map(i => `• ${i}`)] : []), '']),
      sheet.keyTerms.length ? `Açar sözlər: ${sheet.keyTerms.join(', ')}` : '',
    ].join('\n')
    try { await navigator.clipboard.writeText(text); setCopied(true); setTimeout(() => setCopied(false), 1500) } catch { /* clipboard blocked */ }
  }

  return (
    <div className="mx-auto flex w-full max-w-3xl flex-col gap-4 px-4 py-5 sm:py-8">
      <section className="tutor-border rounded-2xl border bg-white p-4 sm:p-5">
        <div className="flex items-start gap-3">
          <span className="grid size-10 shrink-0 place-items-center rounded-xl bg-indigo-50 text-indigo-700" aria-hidden><Timer className="size-5" /></span>
          <div className="min-w-0">
            <h2 className="text-base font-semibold">Son dəqiqə hazırlığı</h2>
            <p className="text-sm text-slate-500">Nə qədər vaxtın var? Mövzu konspekti yalnız sənəddən hazırlanır; suallarda daha çox soruşulan mövzular öndə gəlir.</p>
          </div>
        </div>
        <div className="mt-4 flex flex-col gap-2 sm:flex-row sm:items-center">
          <div role="radiogroup" aria-label="Hazırlıq müddəti" className="grid grid-cols-3 gap-1 rounded-xl bg-slate-100 p-1 sm:inline-grid">
            {durations.map(d => (
              <button key={d} type="button" role="radio" aria-checked={minutes === d} onClick={() => setMinutes(d)}
                className={cn('h-10 rounded-lg px-4 text-sm font-medium whitespace-nowrap transition-colors', minutes === d ? 'bg-white text-slate-900 shadow-sm' : 'text-slate-500 hover:text-slate-800')}>
                {d} dəq
              </button>
            ))}
          </div>
          <button type="button" onClick={run} disabled={disabled || generate.isPending}
            className="btn-primary inline-flex h-11 items-center justify-center rounded-xl px-5 text-sm font-semibold disabled:opacity-50 sm:ml-auto">
            Konspekti hazırla
          </button>
        </div>
      </section>

      {generate.isPending && <Skeleton label="Konspekt hazırlanır" />}
      {generate.isError && <Alert title="Konspekt hazırlanmadı">{generate.error.message}</Alert>}

      {sheet && !generate.isPending && (
        <article className="tutor-enter tutor-border rounded-2xl border bg-white p-4 sm:p-6" aria-label="Mövzu konspekti">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <p className="text-sm text-slate-500">
              {material.data!.minutes} dəqiqəlik konspekt · {sheet.topics.length} mövzu · ~{sheet.readingMinutes} dəq oxu{sheet.sourcePages.length > 0 ? ` · səh. ${sheet.sourcePages.slice(0, 8).join(', ')}${sheet.sourcePages.length > 8 ? '…' : ''}` : ''}
            </p>
            <div className="flex gap-1">
              <button type="button" onClick={copy} className="tutor-border inline-flex h-9 items-center gap-1.5 rounded-lg border px-3 text-sm font-medium text-slate-700 hover:bg-slate-50">
                {copied ? <Check className="size-4 text-emerald-600" aria-hidden /> : <Copy className="size-4" aria-hidden />}Kopyala
              </button>
              <button type="button" onClick={() => save.mutate({ id: material.data!.id, saved: !material.data!.saved })}
                className="tutor-border inline-flex h-9 items-center gap-1.5 rounded-lg border px-3 text-sm font-medium text-slate-700 hover:bg-slate-50">
                {material.data!.saved ? <BookmarkCheck className="size-4 text-indigo-600" aria-hidden /> : <Bookmark className="size-4" aria-hidden />}
                {material.data!.saved ? 'Saxlanılıb' : 'Saxla'}
              </button>
            </div>
          </div>

          <div className="mt-4 flex flex-col gap-3">
            {sheet.topics.map((topic, i) => (
              <section key={topic.title} className="tutor-sunken min-w-0 rounded-xl px-4 py-3.5">
                <h3 className="flex items-baseline gap-2 text-[15px] font-semibold tracking-tight text-slate-900">
                  <span className="text-xs text-slate-400 tabular-nums">{i + 1}</span><span className="min-w-0 break-words">{topic.title}</span>
                </h3>
                <p className="mt-1.5 text-sm leading-relaxed text-slate-700">{topic.explanation}</p>
                {topic.remember.length > 0 && (
                  <div className="mt-2.5">
                    <p className="text-xs font-semibold text-indigo-700">Yadda saxla</p>
                    <ul className="mt-1 space-y-1 text-sm text-slate-800">
                      {topic.remember.map(item => <li key={item} className="flex gap-2"><span className="mt-2 size-1.5 shrink-0 rounded-full bg-indigo-500" aria-hidden />{item}</li>)}
                    </ul>
                  </div>
                )}
                {topic.related.length > 0 && <p className="mt-2 text-xs text-slate-500">Əlaqəli: {topic.related.join(', ')}</p>}
              </section>
            ))}
          </div>

          {sheet.keyTerms.length > 0 && (
            <div className="mt-5">
              <h3 className="text-sm font-semibold">Açar sözlər</h3>
              <div className="mt-2 flex flex-wrap gap-1.5">{sheet.keyTerms.map(t => <span key={t} className="rounded-full bg-indigo-50 px-2.5 py-0.5 text-xs font-medium text-indigo-700">{t}</span>)}</div>
            </div>
          )}
        </article>
      )}

      {previous.length > 1 && (
        <p className="text-xs text-slate-500">
          Əvvəlki konspektlər:{' '}
          {previous.map((m, i) => (
            <button key={m.id} type="button" onClick={() => setChosenId(m.id)} className={cn('mr-2 underline-offset-2 hover:underline', m.id === materialId && 'font-semibold text-slate-800')}>
              {m.minutes} dəq{m.saved ? ' ★' : ''}{i < previous.length - 1 ? ',' : ''}
            </button>
          ))}
        </p>
      )}
    </div>
  )
}
