import { Bookmark, BookmarkCheck, Check, Copy, Layers, ListChecks, Loader2, Trash2 } from 'lucide-react'
import { useMemo, useState } from 'react'
import { Alert } from '../../components/common/Alert'
import { cn } from '../../lib/utils'
import {
  useDeleteMaterial, useMaterial, useMaterials, useOpenQuestions, useSaveMaterial, useSharedAnswers,
  type AnswerLength, type SharedGroup,
} from './api'

type Payload = { groups: SharedGroup[]; auto: boolean }

/** "Ortaq cavab": groups related open questions and writes one answer per group, with what each question still needs. */
export function TutorShared({ chatId, disabled }: { chatId: string; disabled: boolean }) {
  const questions = useOpenQuestions(chatId)
  const materials = useMaterials(chatId)
  const generate = useSharedAnswers(chatId)
  const save = useSaveMaterial(chatId)
  const remove = useDeleteMaterial(chatId)
  const [length, setLength] = useState<AnswerLength>('Short')
  const [picking, setPicking] = useState(false)
  const [picked, setPicked] = useState<number[]>([])
  const [materialId, setMaterialId] = useState<string | null>(null)
  const material = useMaterial<Payload>(materialId)
  const [lastRequest, setLastRequest] = useState<{ auto: boolean; questionNumbers?: number[] } | null>(null)

  const run = (request: { auto: boolean; questionNumbers?: number[] }, len = length) => {
    setLastRequest(request)
    generate.mutate({ ...request, length: len }, { onSuccess: m => { setMaterialId(m.id); setPicking(false) } })
  }
  const switchLength = (len: AnswerLength) => {
    setLength(len)
    if (lastRequest && material.data && material.data.length !== len) run(lastRequest, len)
  }
  const shared = useMemo(() => (materials.data ?? []).filter(m => m.kind === 'SharedAnswer'), [materials.data])

  if (questions.isLoading) return <Skeleton label="Suallar oxunur" />
  if (questions.isError) return <div className="p-4"><Alert title="Suallar oxunmadı">{questions.error.message}</Alert></div>
  const data = questions.data!

  return (
    <div className="mx-auto flex w-full max-w-3xl flex-col gap-4 px-4 py-5 sm:py-8">
      {!data.supported ? (
        <div className="tutor-border rounded-2xl border bg-white px-5 py-8 text-center">
          <span className="mx-auto grid size-11 place-items-center rounded-xl bg-amber-50 text-amber-700" aria-hidden><Layers className="size-5" /></span>
          <h2 className="mt-3 text-lg font-semibold">Ortaq cavab bu sənəd üçün əlçatan deyil</h2>
          <p className="mx-auto mt-1 max-w-md text-sm text-slate-600">{data.warning}</p>
        </div>
      ) : (
        <section className="tutor-border rounded-2xl border bg-white p-4 sm:p-5">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div className="min-w-0">
              <h2 className="text-base font-semibold">Əlaqəli suallara bir cavab</h2>
              <p className="text-sm text-slate-500">{data.questions.length} açıq sual tapıldı{data.warning ? ` · ${data.warning}` : ''}</p>
            </div>
            <LengthToggle value={length} onChange={switchLength} disabled={generate.isPending} />
          </div>
          <div className="mt-4 flex flex-col gap-2 sm:flex-row">
            <button type="button" onClick={() => run({ auto: true })} disabled={disabled || generate.isPending || data.questions.length < 2}
              className="btn-primary inline-flex h-11 items-center justify-center gap-2 rounded-xl px-4 text-sm font-semibold disabled:opacity-50">
              {generate.isPending && lastRequest?.auto ? <Loader2 className="size-4 animate-spin" aria-hidden /> : <Layers className="size-4" aria-hidden />}
              Ortaq cavabları yarat
            </button>
            <button type="button" onClick={() => setPicking(v => !v)} aria-expanded={picking} disabled={generate.isPending}
              className="tutor-border inline-flex h-11 items-center justify-center gap-2 rounded-xl border px-4 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-50">
              <ListChecks className="size-4" aria-hidden />Sualları özüm seçim
            </button>
          </div>

          {picking && (
            <div className="mt-4">
              <ul className="tutor-border max-h-[min(50dvh,24rem)] divide-y overflow-y-auto rounded-xl border" style={{ borderColor: 'var(--app-border)' }}>
                {data.questions.map(q => {
                  const checked = picked.includes(q.number)
                  return (
                    <li key={q.number}>
                      <label className="flex min-h-11 cursor-pointer items-start gap-3 px-3 py-2.5 text-sm hover:bg-slate-50">
                        <input type="checkbox" checked={checked} className="mt-0.5 size-4 shrink-0 accent-indigo-600"
                          onChange={() => setPicked(list => checked ? list.filter(n => n !== q.number) : [...list, q.number].slice(0, 40))} />
                        <span className="w-6 shrink-0 text-right font-semibold text-slate-400 tabular-nums">{q.number}.</span>
                        <span className="min-w-0 flex-1 text-slate-800">{q.text}</span>
                      </label>
                    </li>
                  )
                })}
              </ul>
              <div className="mt-3 flex flex-wrap items-center justify-end gap-2">
                {picked.length > 0 && <button type="button" onClick={() => setPicked([])} className="h-10 px-3 text-sm text-slate-500 hover:text-slate-900">Təmizlə</button>}
                <button type="button" onClick={() => run({ auto: false, questionNumbers: picked })} disabled={disabled || picked.length < 2 || generate.isPending}
                  className="btn-primary inline-flex h-10 items-center gap-2 rounded-xl px-4 text-sm font-semibold disabled:opacity-50">
                  {generate.isPending && !lastRequest?.auto && <Loader2 className="size-4 animate-spin" aria-hidden />}
                  Ortaq cavab yarat ({picked.length})
                </button>
              </div>
            </div>
          )}
        </section>
      )}

      {generate.isPending && <Skeleton label="Ortaq cavablar hazırlanır — bu 10–30 saniyə çəkə bilər" />}
      {generate.isError && <Alert title="Ortaq cavab yaradılmadı">{generate.error.message}</Alert>}

      {material.data && !generate.isPending && (
        <section className="flex flex-col gap-3" aria-label="Ortaq cavablar">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <p className="text-sm text-slate-500">
              {material.data.payload.groups.length} qrup · {material.data.length === 'Long' ? 'Geniş' : 'Qısa'}{material.data.fromCache && ' · yadda saxlanmış nəticə'}
            </p>
            <button type="button" onClick={() => save.mutate({ id: material.data!.id, saved: !material.data!.saved })}
              className="tutor-border inline-flex h-9 items-center gap-1.5 rounded-lg border px-3 text-sm font-medium text-slate-700 hover:bg-slate-50">
              {material.data.saved ? <BookmarkCheck className="size-4 text-indigo-600" aria-hidden /> : <Bookmark className="size-4" aria-hidden />}
              {material.data.saved ? 'Saxlanılıb' : 'Saxla'}
            </button>
          </div>
          {material.data.payload.groups.map((group, i) => <GroupCard key={`${material.data!.id}-${i}`} group={group} />)}
        </section>
      )}

      {shared.length > 0 && (
        <section className="tutor-border rounded-2xl border bg-white p-4">
          <h3 className="text-sm font-semibold">Əvvəlki ortaq cavablar</h3>
          <ul className="mt-2 space-y-1">
            {shared.map(m => (
              <li key={m.id} className={cn('flex items-center gap-2 rounded-lg px-2 py-1.5', m.id === materialId && 'bg-indigo-50/60')}>
                <button type="button" onClick={() => setMaterialId(m.id)} className="min-w-0 flex-1 truncate text-left text-sm text-slate-700 hover:text-slate-900">
                  {m.saved && <BookmarkCheck className="mr-1 inline size-3.5 text-indigo-600" aria-label="Saxlanılıb" />}
                  {m.title} <span className="text-xs text-slate-400">· {m.items} qrup · {m.length === 'Long' ? 'geniş' : 'qısa'}</span>
                </button>
                <button type="button" onClick={() => { remove.mutate(m.id); if (m.id === materialId) setMaterialId(null) }} aria-label="Sil"
                  className="grid size-8 shrink-0 place-items-center rounded-md text-slate-400 hover:bg-rose-50 hover:text-rose-600"><Trash2 className="size-3.5" /></button>
              </li>
            ))}
          </ul>
        </section>
      )}
    </div>
  )
}

function GroupCard({ group }: { group: SharedGroup }) {
  const [copied, setCopied] = useState(false)
  const copy = async () => {
    const text = [group.title, '', group.questions.map(q => `${q.number}. ${q.text}`).join('\n'), '', group.answer,
      group.keywords.length ? `\nAçar sözlər: ${group.keywords.join(', ')}` : ''].join('\n')
    try { await navigator.clipboard.writeText(text); setCopied(true); setTimeout(() => setCopied(false), 1500) } catch { /* clipboard blocked */ }
  }
  return (
    <article className="tutor-enter tutor-border rounded-2xl border bg-white p-4 sm:p-5">
      <div className="flex items-start justify-between gap-3">
        <h3 className="min-w-0 text-base font-semibold break-words">{group.title}</h3>
        <button type="button" onClick={copy} aria-label="Kopyala" title="Kopyala"
          className="grid size-9 shrink-0 place-items-center rounded-lg text-slate-500 hover:bg-slate-100 hover:text-slate-900">
          {copied ? <Check className="size-4 text-emerald-600" /> : <Copy className="size-4" />}
        </button>
      </div>
      <ul className="mt-2 space-y-1.5">
        {group.questions.map(q => (
          <li key={q.number} className="text-sm">
            <div className="flex items-start gap-2">
              <span className={cn('mt-0.5 shrink-0 rounded-md px-1.5 py-px text-[11px] font-semibold', q.coverage === 'FULL' ? 'bg-emerald-50 text-emerald-700' : 'bg-amber-50 text-amber-800')}
                title={q.coverage === 'FULL' ? 'Ortaq cavab bu sualı tam əhatə edir' : 'Bu sual üçün əlavə məqamlar lazımdır'}>
                {q.coverage === 'FULL' ? 'Tam' : 'Qismən'}
              </span>
              <span className="min-w-0 flex-1 text-slate-700"><b className="font-semibold text-slate-500 tabular-nums">{q.number}.</b> {q.text}</span>
            </div>
            {q.extraPoints.length > 0 && (
              <ul className="mt-1 ml-[3.4rem] list-disc space-y-0.5 pl-4 text-xs text-amber-900">{q.extraPoints.map(p => <li key={p}>{p}</li>)}</ul>
            )}
          </li>
        ))}
      </ul>
      {group.answer && <p className="tutor-sunken mt-3 rounded-xl px-3.5 py-3 text-[15px] leading-relaxed whitespace-pre-line text-slate-800">{group.answer}</p>}
      <div className="mt-3 flex flex-wrap items-center gap-1.5">
        {group.keywords.map(k => <span key={k} className="rounded-full bg-indigo-50 px-2.5 py-0.5 text-xs font-medium text-indigo-700">{k}</span>)}
        {group.sourcePages.length > 0 && <span className="ml-auto text-xs text-slate-400">səh. {group.sourcePages.join(', ')}</span>}
      </div>
    </article>
  )
}

function LengthToggle({ value, onChange, disabled }: { value: AnswerLength; onChange: (v: AnswerLength) => void; disabled?: boolean }) {
  return (
    <div role="radiogroup" aria-label="Cavab uzunluğu" className="flex shrink-0 rounded-lg bg-slate-100 p-0.5 text-xs font-medium">
      {([['Short', 'Qısa'], ['Long', 'Geniş']] as const).map(([v, label]) => (
        <button key={v} type="button" role="radio" aria-checked={value === v} disabled={disabled} onClick={() => onChange(v)}
          className={cn('h-8 rounded-md px-3 whitespace-nowrap transition-colors', value === v ? 'bg-white text-slate-900 shadow-sm' : 'text-slate-500 hover:text-slate-800')}>
          {label}
        </button>
      ))}
    </div>
  )
}

export function Skeleton({ label }: { label: string }) {
  return (
    <div className="tutor-border space-y-3 rounded-2xl border bg-white p-5" role="status" aria-label={label}>
      <p className="flex items-center gap-2 text-sm text-slate-500"><Loader2 className="size-4 animate-spin" aria-hidden />{label}</p>
      <div className="h-4 w-1/3 animate-pulse rounded bg-slate-100" />
      <div className="h-4 w-full animate-pulse rounded bg-slate-100" />
      <div className="h-4 w-5/6 animate-pulse rounded bg-slate-100" />
    </div>
  )
}
