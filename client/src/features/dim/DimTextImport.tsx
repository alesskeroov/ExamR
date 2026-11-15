import { ClipboardPaste, Eye, Save } from 'lucide-react'
import { useState } from 'react'
import { Alert } from '../../components/common/Alert'
import { Button } from '../../components/common/Button'
import { cn } from '../../lib/utils'
import { useDimCatalog, useImportDimText, type DimImportResult, type DimSourceDocument } from './api'
import { DimSetPicker } from './DimSetPicker'
import { inputClass } from './ui'

const textareaClass = 'w-full rounded-lg border border-slate-300 bg-white p-3 font-mono text-sm focus:border-indigo-500 focus:ring-2 focus:ring-indigo-500/20 focus:outline-none'

/**
 * A whole test pasted from Word, a web page or a PDF viewer: checked in a preview first, then saved into a manual set in
 * one go. Questions with a detected answer go straight into the bank; the rest get their answers from the answer key.
 */
export function DimTextImport({ documents, documentId, onDocument, onImported }: {
  documents: DimSourceDocument[]
  documentId: string
  onDocument: (id: string) => void
  /** Called after a save with the subject used, so the document panel can offer it for the rest of the questions. */
  onImported?: (documentId: string, subject: string) => void
}) {
  const catalog = useDimCatalog()
  const importText = useImportDimText()
  const [subject, setSubject] = useState('')
  const [topic, setTopic] = useState('')
  const [text, setText] = useState('')
  const [preview, setPreview] = useState<DimImportResult | null>(null)
  const [saved, setSaved] = useState<DimImportResult | null>(null)
  const set = documents.find(d => d.id === documentId && d.isManual)
  const ready = !!set && subject !== '' && text.trim().length >= 2

  function run(asPreview: boolean) {
    if (!set) return
    setSaved(null)
    importText.mutate({ documentId: set.id, text, track: 'Buraxilis', subject, topic: topic.trim() || null, preview: asPreview }, {
      onSuccess: result => {
        if (asPreview) setPreview(result)
        else { setSaved(result); setPreview(null); setText(''); onImported?.(set.id, subject) }
      },
    })
  }

  const open = preview?.questions.filter(q => q.options.length === 0).length ?? 0
  const missing = preview?.questions.filter(q => q.options.length > 0 && q.correctIndex === null).length ?? 0

  return (
    <div className="mt-3 flex flex-col gap-4">
      <div className="grid gap-3 sm:grid-cols-2">
        <DimSetPicker documents={documents} documentId={documentId} onDocument={onDocument} onlyManual />
        <label className="flex flex-col gap-1.5 text-sm font-medium text-slate-700">Fənn
          <select className={inputClass} value={subject} onChange={e => setSubject(e.target.value)}>
            <option value="">Fənn seçin</option>
            {(catalog.data?.buraxilis ?? []).map(s => <option key={s.code} value={s.code}>{s.name}</option>)}
          </select>
        </label>
        <label className="flex flex-col gap-1.5 text-sm font-medium text-slate-700">Mövzu (istəyə bağlı, hamısına)
          <input className={inputClass} value={topic} maxLength={200} onChange={e => setTopic(e.target.value)} />
        </label>
      </div>

      <div className="flex flex-col gap-1.5">
        <label className="flex flex-col gap-1.5 text-sm font-medium text-slate-700">Test mətni
          <textarea value={text} rows={12} maxLength={200_000} className={textareaClass} aria-describedby="dim-import-hint"
            placeholder={'1. 2 + 2 neçədir?\nA) 3  B) 4  C) 5  D) 6  E) 7\n2. …\n14. 1/2 + 2 cəmini tapın.'}
            onChange={e => { setText(e.target.value); setPreview(null) }} />
        </label>
        <p id="dim-import-hint" className="text-xs text-slate-500">
          Hər sual nömrə ilə (1. və ya 1)) başlasın, variantlar A) B) … ilə (ayrı sətirdə və ya bir sətirdə). Variantı olmayan sual
          açıq (kodlaşdırılan) sual kimi düşür. Düzgün cavabları sonra cavab açarı ilə verin: <code>1-B 2-A 14=2,5</code>.
          Boş dəstdə nömrələr mətndəki kimi qalır.
        </p>
      </div>

      {!set && <p className="text-sm text-amber-700">Mətn yalnız əl ilə yaradılmış dəstə idxal olunur: yuxarıda dəst seçin və ya yaradın.</p>}
      {importText.isError && <Alert title="İdxal alınmadı">{importText.error.message}</Alert>}

      <div className="flex flex-wrap items-center gap-3">
        <Button variant="secondary" loading={importText.isPending && !preview} disabled={!ready} onClick={() => run(true)}>
          <Eye className="size-4" aria-hidden />Önizlə
        </Button>
        <Button loading={importText.isPending && !!preview} disabled={!ready || !preview} onClick={() => run(false)}>
          <Save className="size-4" aria-hidden />{preview ? `${preview.questions.length} sualı saxla` : 'Saxla'}
        </Button>
        {saved && (
          <p role="status" className="text-sm text-emerald-700">
            {saved.saved} sual əlavə edildi, {saved.banked} banka düşdü.{saved.saved > saved.banked && ' Qalanların cavabını aşağıda cavab açarı ilə verin.'}
          </p>
        )}
      </div>

      {preview && (
        <section aria-label="Önizləmə" className="rounded-lg border border-slate-200">
          <p className="flex flex-wrap gap-x-3 gap-y-1 border-b border-slate-100 px-3 py-2 text-sm text-slate-600">
            <ClipboardPaste className="size-4 self-center text-indigo-600" aria-hidden />
            <span className="font-medium text-slate-800">{preview.questions.length} sual</span>
            <span>{preview.questions.length - open} qapalı</span>
            <span>{open} açıq</span>
            {missing > 0 && <span className="text-amber-700">{missing} qapalının cavabı tapılmadı</span>}
          </p>
          {preview.warnings.length > 0 && <p className="border-b border-slate-100 px-3 py-2 text-xs text-amber-700">{preview.warnings.join(' · ')}</p>}
          <ol className="max-h-96 divide-y divide-slate-100 overflow-y-auto text-sm">
            {preview.questions.map(q => (
              <li key={q.number} className="px-3 py-2">
                <p className="text-slate-800"><span className="mr-1.5 font-semibold text-slate-500 tabular-nums">#{q.number}</span>{q.text}</p>
                {q.options.length > 0 ? (
                  <p className="mt-1 flex flex-wrap gap-x-3 gap-y-0.5 text-slate-600">
                    {q.options.map((o, i) => (
                      <span key={i} className={cn(q.correctIndex === i && 'font-medium text-emerald-700')}>{String.fromCharCode(65 + i)}) {o}</span>
                    ))}
                  </p>
                ) : <p className="mt-1 text-xs text-indigo-700">Açıq sual: cavabını açarla verin</p>}
                {q.warning && <p className="mt-1 text-xs text-amber-700">{q.warning}</p>}
              </li>
            ))}
          </ol>
        </section>
      )}
    </div>
  )
}
