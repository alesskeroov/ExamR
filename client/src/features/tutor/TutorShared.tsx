import { Bookmark, BookmarkCheck, Check, Copy, Loader2, RotateCw } from 'lucide-react'
import { useState } from 'react'
import { Alert } from '../../components/common/Alert'
import { useMaterial, useMaterials, useSaveMaterial, useSharedAnswers, type SharedPayload } from './api'

const generatorVersion = 'shared-v3'

/** One document, one main topic, one exam answer. */
export function TutorShared({ chatId, disabled }: { chatId: string; disabled: boolean }) {
  const materials = useMaterials(chatId)
  const generate = useSharedAnswers(chatId)
  const save = useSaveMaterial(chatId)
  const [chosenId, setChosenId] = useState<string | null>(null)
  const [copied, setCopied] = useState(false)
  const [copyError, setCopyError] = useState(false)
  const previous = (materials.data ?? [])
    .filter(m => m.kind === 'SharedAnswer' && m.generatorVersion === generatorVersion)
    .sort((a, b) => b.createdAt.localeCompare(a.createdAt))
  const material = useMaterial<SharedPayload>(chosenId ?? previous[0]?.id ?? null)
  const result = material.data?.payload.generatorVersion === generatorVersion ? material.data : null
  const busy = generate.isPending || materials.isLoading || material.isLoading && !!(chosenId ?? previous[0]?.id)
  const run = (regenerate: boolean) => {
    setCopied(false)
    setCopyError(false)
    generate.mutate({ regenerate }, { onSuccess: m => setChosenId(m.id) })
  }
  const copy = async () => {
    if (!result) return
    try {
      await navigator.clipboard.writeText(`${result.payload.mainTopic}\n\n${result.payload.answer}`)
      setCopyError(false)
      setCopied(true)
      setTimeout(() => setCopied(false), 1500)
    } catch { setCopyError(true) }
  }

  return (
    <div className="tutor-shared mx-auto flex w-full min-w-0 max-w-3xl flex-col gap-4 px-4 py-5 sm:py-8">
      <section className="tutor-shared-card rounded-2xl border p-4 sm:p-6">
        <h2 className="text-lg font-semibold">Ortaq cavab hazırla</h2>
        <p className="tutor-shared-muted mt-2 text-sm leading-relaxed">Sənədin əsas mövzusunu analiz edərək imtahana hazırlıq üçün ümumi cavab yarat.</p>
        {!result && !busy && (
          <button type="button" onClick={() => run(false)} disabled={disabled} className="btn-primary mt-4 min-h-11 w-full rounded-xl px-5 py-2.5 text-sm font-semibold disabled:opacity-50 sm:w-auto">
            Ortaq cavab yarat
          </button>
        )}
        {disabled && <p className="tutor-shared-muted mt-3 text-sm">AI köməkçisi hazırda aktiv deyil.</p>}
      </section>

      {busy && <div className="tutor-shared-card rounded-2xl border p-5" role="status" aria-live="polite">
        <p className="tutor-shared-muted flex items-center gap-2 text-sm"><Loader2 className="size-4 shrink-0 animate-spin" aria-hidden />{generate.isPending ? 'Sənəd analiz edilir, ortaq cavab hazırlanır…' : 'Saxlanmış cavab yüklənir…'}</p>
      </div>}
      {materials.isError && <Alert title="Saxlanmış cavab yüklənmədi">{materials.error.message}</Alert>}
      {material.isError && <Alert title="Cavab açılmadı">{material.error.message}</Alert>}
      {generate.isError && <Alert title="Ortaq cavab hazırlanmadı">{generate.error.message}</Alert>}
      {save.isError && <Alert title="Cavab saxlanmadı">{save.error.message}</Alert>}
      {copyError && <Alert title="Cavab kopyalanmadı">Brauzer kopyalamağa icazə vermədi. Mətni seçib kopyalaya bilərsiniz.</Alert>}

      {result && <article className="tutor-shared-card tutor-enter rounded-2xl border p-4 sm:p-6" aria-label="Ortaq imtahan cavabı" aria-busy={generate.isPending}>
        <p className="tutor-shared-muted text-xs font-semibold tracking-wider">ƏSAS MÖVZU</p>
        <h3 className="mt-2 text-xl font-semibold leading-snug">{result.payload.mainTopic}</h3>
        <h4 className="tutor-shared-muted mt-6 text-xs font-semibold tracking-wider">ORTAQ İMTAHAN CAVABI</h4>
        <p className="mt-3 text-[15px] leading-[1.85] whitespace-pre-line sm:text-base">{result.payload.answer}</p>
        {result.payload.sourcePages.length > 0 && <p className="tutor-shared-muted mt-4 text-xs leading-relaxed">Mənbə səhifələri: {result.payload.sourcePages.join(', ')}</p>}
        <div className="mt-6 grid grid-cols-1 gap-2 sm:grid-cols-3">
          <button type="button" onClick={copy} className="tutor-shared-action"><span aria-hidden>{copied ? <Check className="size-4" /> : <Copy className="size-4" />}</span>{copied ? 'Kopyalandı' : 'Kopyala'}</button>
          <button type="button" onClick={() => run(true)} disabled={disabled || busy} className="tutor-shared-action"><RotateCw className="size-4 shrink-0" aria-hidden />Yenidən yarat</button>
          <button type="button" onClick={() => save.mutate({ id: result.id, saved: !result.saved })} disabled={save.isPending || busy} aria-pressed={result.saved} className="tutor-shared-action">
            {result.saved ? <BookmarkCheck className="size-4 shrink-0" aria-hidden /> : <Bookmark className="size-4 shrink-0" aria-hidden />}{result.saved ? 'Saxlanılıb' : 'Saxla'}
          </button>
        </div>
      </article>}
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
