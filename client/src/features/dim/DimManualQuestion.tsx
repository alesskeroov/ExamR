import { Plus, Save } from 'lucide-react'
import { useRef, useState, type KeyboardEvent } from 'react'
import { Alert } from '../../components/common/Alert'
import { Button } from '../../components/common/Button'
import { cn } from '../../lib/utils'
import { useCreateDimQuestion, useCreateDimSet, useDimCatalog, type DimSourceDocument } from './api'
import { inputClass } from './ui'

type Kind = 'closed' | 'coded'

const LABELS = ['A', 'B', 'C', 'D', 'E'] as const
const emptyOptions = () => LABELS.map(() => '')
const textareaClass = 'w-full rounded-lg border border-slate-300 bg-white p-3 text-sm focus:border-indigo-500 focus:ring-2 focus:ring-indigo-500/20 focus:outline-none'

/**
 * Splits a pasted question with its options ("…\nA) 12\nB) 15 …") into the stem and the option texts.
 * Null when fewer than two options are found, so ordinary text is pasted as is.
 */
function splitPastedQuestion(pasted: string): { text: string; options: string[] } | null {
  const marker = /(?:^|\s)([A-E])\s*[).]\s*/g
  const found: { label: string; start: number; end: number }[] = []
  for (const match of pasted.matchAll(marker)) {
    const expected = LABELS[found.length]
    if (match[1] !== expected) continue
    found.push({ label: match[1], start: match.index, end: match.index + match[0].length })
  }
  if (found.length < 2) return null
  const options = found.map((f, i) => pasted.slice(f.end, i + 1 < found.length ? found[i + 1].start : undefined).trim())
  return { text: pasted.slice(0, found[0].start).trim(), options }
}

/**
 * Types questions in by hand, one after another: the set, subject and type stay selected and the form clears after
 * each save, so a whole test can be entered quickly. Ctrl+Enter saves.
 */
export function DimManualQuestion({ documents, documentId, onDocument }: {
  documents: DimSourceDocument[]
  documentId: string
  onDocument: (id: string) => void
}) {
  const catalog = useDimCatalog()
  const create = useCreateDimQuestion()
  const createSet = useCreateDimSet()
  const textRef = useRef<HTMLTextAreaElement>(null)
  const [newSetTitle, setNewSetTitle] = useState('')
  const [subject, setSubject] = useState('')
  const [topic, setTopic] = useState('')
  const [kind, setKind] = useState<Kind>('closed')
  const [text, setText] = useState('')
  const [options, setOptions] = useState<string[]>(emptyOptions)
  const [correct, setCorrect] = useState<number | null>(null)
  const [answer, setAnswer] = useState('')
  const [saved, setSaved] = useState<number | null>(null)

  const subjects = catalog.data?.buraxilis ?? []
  const spec = subjects.find(s => s.code === subject)
  const usedOptions = options.slice(0, lastFilled(options) + 1)
  const ready = documentId !== '' && subject !== '' && text.trim() !== '' && (kind === 'coded'
    ? answer.trim() !== ''
    : usedOptions.length >= 2 && usedOptions.every(o => o.trim() !== '') && correct !== null && correct < usedOptions.length)

  function save() {
    if (!ready || create.isPending) return
    create.mutate({
      documentId, track: 'Buraxilis', subject, topic: topic.trim() || null, text: text.trim(),
      options: kind === 'coded' ? [] : usedOptions.map(o => o.trim()),
      correctIndex: kind === 'coded' ? null : correct,
      codedAnswer: kind === 'coded' ? answer.trim() : null,
    }, {
      onSuccess: question => {
        setSaved(question.sourceNumber)
        setText('')
        setOptions(emptyOptions())
        setCorrect(null)
        setAnswer('')
        textRef.current?.focus()
      },
    })
  }

  function onKeyDown(event: KeyboardEvent) {
    if (event.key === 'Enter' && (event.ctrlKey || event.metaKey)) {
      event.preventDefault()
      save()
    }
  }

  function addSet() {
    createSet.mutate(newSetTitle.trim(), { onSuccess: set => { setNewSetTitle(''); onDocument(set.id) } })
  }

  return (
    <div className="mt-3 flex flex-col gap-4" onKeyDown={onKeyDown}>
      <div className="grid gap-3 sm:grid-cols-2">
        <label className="flex flex-col gap-1.5 text-sm font-medium text-slate-700">Dəst / sənəd
          <select className={inputClass} value={documentId} onChange={e => onDocument(e.target.value)}>
            <option value="">Seçin</option>
            {documents.map(d => <option key={d.id} value={d.id}>{d.isManual ? '✎ ' : ''}{d.originalFileName}</option>)}
          </select>
        </label>
        <div className="flex flex-col gap-1.5 text-sm font-medium text-slate-700">
          <label htmlFor="dim-new-set">və ya yeni dəst</label>
          <div className="flex gap-2">
            <input id="dim-new-set" className={inputClass} value={newSetTitle} maxLength={200} placeholder="məs. Riyaziyyat — sınaq 1"
              onChange={e => setNewSetTitle(e.target.value)} />
            <Button variant="secondary" className="h-10 shrink-0" loading={createSet.isPending} disabled={newSetTitle.trim().length < 2}
              onClick={addSet}><Plus className="size-4" aria-hidden />Yarat</Button>
          </div>
        </div>
        <label className="flex flex-col gap-1.5 text-sm font-medium text-slate-700">Fənn
          <select className={inputClass} value={subject} onChange={e => setSubject(e.target.value)}>
            <option value="">Fənn seçin</option>
            {subjects.map(s => <option key={s.code} value={s.code}>{s.name}</option>)}
          </select>
        </label>
        <label className="flex flex-col gap-1.5 text-sm font-medium text-slate-700">Mövzu (istəyə bağlı)
          <input className={inputClass} value={topic} maxLength={200} placeholder="məs. Kvadrat tənliklər" onChange={e => setTopic(e.target.value)} />
        </label>
      </div>
      {createSet.isError && <Alert title="Dəst yaradılmadı">{createSet.error.message}</Alert>}

      <div role="radiogroup" aria-label="Sualın növü" className="inline-flex self-start rounded-lg border border-slate-200 bg-white p-1">
        {([['closed', 'Qapalı (A–E)'], ['coded', 'Açıq — kodlaşdırılan']] as const).map(([value, label]) => (
          <button key={value} type="button" role="radio" aria-checked={kind === value} onClick={() => setKind(value)}
            className={cn('rounded-md px-3 py-1.5 text-sm font-medium whitespace-nowrap', kind === value ? 'bg-indigo-600 text-white' : 'text-slate-600 hover:bg-slate-100')}>
            {label}
          </button>
        ))}
      </div>
      {spec && (
        <p className="-mt-2 text-xs text-slate-500">
          Rəsmi imtahanda {spec.name}: {spec.closed} qapalı{spec.coded > 0 && `, ${spec.coded} kodlaşdırılan`}{spec.written > 0 && `, ${spec.written} yazılı`} tapşırıq.
        </p>
      )}

      <div className="flex flex-col gap-1.5">
      <label className="flex flex-col gap-1.5 text-sm font-medium text-slate-700">Sual
        <textarea ref={textRef} value={text} rows={4} maxLength={4000} className={textareaClass}
          aria-describedby={kind === 'closed' ? 'dim-manual-paste-hint' : undefined}
          onChange={e => setText(e.target.value)}
          onPaste={e => {
            if (kind !== 'closed') return
            const split = splitPastedQuestion(e.clipboardData.getData('text'))
            if (!split) return
            e.preventDefault()
            setText(split.text)
            setOptions(LABELS.map((_, i) => split.options[i] ?? ''))
          }} />
      </label>
        {kind === 'closed' && <p id="dim-manual-paste-hint" className="text-xs text-slate-500">Sualı variantları ilə birlikdə yapışdırsanız (A) … B) …), variantlar avtomatik ayrılır.</p>}
      </div>

      {kind === 'closed' ? (
        <fieldset className="flex flex-col gap-2">
          <legend className="mb-1 text-sm font-medium text-slate-700">Variantlar — düzgün cavabı işarələyin (boş qalan son variantlar nəzərə alınmır)</legend>
          {options.map((o, i) => (
            <div key={LABELS[i]} className="flex items-center gap-2">
              <input type="radio" name="dim-manual-correct" className="size-4 shrink-0 accent-emerald-600" checked={correct === i}
                onChange={() => setCorrect(i)} aria-label={`${LABELS[i]} düzgün cavabdır`} />
              <span className="w-5 text-sm font-semibold text-slate-500">{LABELS[i]})</span>
              <input className={inputClass} value={o} maxLength={1000} aria-label={`Variant ${LABELS[i]}`}
                onChange={e => setOptions(list => list.map((x, j) => (j === i ? e.target.value : x)))} />
            </div>
          ))}
        </fieldset>
      ) : (
        <div className="flex flex-col gap-1.5 sm:max-w-md">
          <label className="flex flex-col gap-1.5 text-sm font-medium text-slate-700">Düzgün cavab
            <input className={inputClass} value={answer} maxLength={100} inputMode="decimal" placeholder="məs. 2,5" aria-describedby="dim-manual-answer-hint"
              onChange={e => setAnswer(e.target.value)} />
          </label>
          <p id="dim-manual-answer-hint" className="text-xs text-slate-500">
            Tam ədəd, onluq (2,5) və ya adi kəsr (5/2). Bir neçə düzgün yazılış varsa nöqtəli vergüllə ayırın: <code>2,5; 5/2</code>.
            Şagirdin cavabı ədəd kimi müqayisə olunur: 2,5 = 2.5 = 5/2.
          </p>
        </div>
      )}

      {create.isError && <Alert title="Sual əlavə edilmədi">{create.error.message}</Alert>}
      <div className="flex flex-wrap items-center gap-3">
        <Button loading={create.isPending} disabled={!ready} onClick={save}><Save className="size-4" aria-hidden />Əlavə et</Button>
        <span className="text-xs text-slate-500">Ctrl + Enter</span>
        {saved !== null && <p role="status" className="text-sm text-emerald-700">#{saved} sual banka əlavə edildi.</p>}
      </div>
    </div>
  )
}

function lastFilled(options: string[]) {
  for (let i = options.length - 1; i >= 0; i--) if (options[i].trim() !== '') return i
  return -1
}
