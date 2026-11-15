import { Headphones, Pencil, Tags, Trash2, Upload } from 'lucide-react'
import { useRef, useState } from 'react'
import { Alert } from '../../components/common/Alert'
import { Button } from '../../components/common/Button'
import {
  DIM_AUDIO_MAX_BYTES, dimAudioAdminUrl, useDeleteDimAudio, useDimAudios, useSetDimTopics, useUpdateDimAudio, useUploadDimAudio,
  type DimAudio,
} from './api'
import { inputClass } from './ui'

const numberClass = inputClass.replace('w-full', 'w-20')

/**
 * Listening recordings of a document (e.g. the English section): each covers a range of question numbers, and the
 * student can play it on those questions during the exam.
 */
export function DimAudioPanel({ documentId }: { documentId: string }) {
  const audios = useDimAudios(documentId)
  const upload = useUploadDimAudio()
  const fileRef = useRef<HTMLInputElement>(null)
  const [file, setFile] = useState<File | null>(null)
  const [title, setTitle] = useState('')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [clientError, setClientError] = useState<string | null>(null)
  const range = parseRange(from, to)

  function pick(next: File | null) {
    setClientError(null)
    if (next && next.size > DIM_AUDIO_MAX_BYTES) {
      setClientError('Səs faylı 30 MB-dan böyük ola bilməz.')
      next = null
    }
    setFile(next)
    if (next && !title) setTitle(next.name.replace(/\.[^.]+$/, ''))
  }

  function submit() {
    if (!file || !range) return
    upload.mutate({ documentId, file, title: title.trim(), questionFrom: range.from, questionTo: range.to }, {
      onSuccess: () => {
        setFile(null); setTitle(''); setFrom(''); setTo('')
        if (fileRef.current) fileRef.current.value = ''
      },
    })
  }

  return (
    <details className="mt-4 border-t border-slate-100 pt-3 text-sm" open={(audios.data?.length ?? 0) > 0 || undefined}>
      <summary className="flex cursor-pointer items-center gap-2 font-medium text-slate-700">
        <Headphones className="size-4 text-indigo-600" aria-hidden />Səs materialları (dinləmə)
        {audios.data && audios.data.length > 0 && <span className="font-normal text-slate-500">— {audios.data.length} fayl</span>}
      </summary>
      <p className="mt-2 text-slate-600">
        Səs faylını yükləyin və hansı suallara aid olduğunu göstərin. İmtahanda həmin suallarda pleyer görünəcək; eyni səsə aid
        suallar arasında keçəndə səs dayanmır. MP3, M4A, OGG və ya WAV, ən çox 30 MB.
      </p>

      {audios.isError && <div className="mt-2"><Alert title="Səs faylları yüklənmədi">{audios.error.message}</Alert></div>}
      {audios.data && audios.data.length > 0 && (
        <ul className="mt-3 flex flex-col gap-2">{audios.data.map(a => <li key={a.id}><AudioRow audio={a} /></li>)}</ul>
      )}

      <div className="mt-3 grid gap-3 rounded-lg border border-dashed border-slate-300 p-3 sm:grid-cols-[minmax(0,1fr)_minmax(0,1fr)_auto]">
        <label className="flex flex-col gap-1.5 font-medium text-slate-700">Fayl
          <input ref={fileRef} type="file" accept="audio/mpeg,audio/mp4,audio/x-m4a,audio/ogg,audio/wav,.mp3,.m4a,.ogg,.wav"
            onChange={e => pick(e.target.files?.[0] ?? null)}
            className="text-sm file:mr-3 file:rounded-md file:border-0 file:bg-indigo-50 file:px-3 file:py-2 file:font-medium file:text-indigo-700" />
        </label>
        <label className="flex flex-col gap-1.5 font-medium text-slate-700">Ad
          <input className={inputClass} value={title} maxLength={200} placeholder="məs. Listening 1" onChange={e => setTitle(e.target.value)} />
        </label>
        <fieldset className="flex flex-col gap-1.5">
          <legend className="mb-1.5 font-medium text-slate-700">Suallar</legend>
          <div className="flex items-center gap-2">
            <input className={numberClass} inputMode="numeric" value={from} placeholder="1" aria-label="İlk sual nömrəsi" onChange={e => setFrom(e.target.value)} />
            <span aria-hidden>–</span>
            <input className={numberClass} inputMode="numeric" value={to} placeholder="5" aria-label="Son sual nömrəsi" onChange={e => setTo(e.target.value)} />
          </div>
        </fieldset>
        <div className="flex flex-wrap items-center gap-3 sm:col-span-3">
          <Button variant="secondary" loading={upload.isPending} disabled={!file || !range} onClick={submit}>
            <Upload className="size-4" aria-hidden />Səsi yüklə
          </Button>
          {upload.isPending && <span role="status" className="text-slate-500">Yüklənir…</span>}
        </div>
      </div>
      {(clientError || upload.isError) && <div className="mt-2"><Alert title="Səs yüklənmədi">{clientError ?? upload.error?.message}</Alert></div>}
    </details>
  )
}

function AudioRow({ audio }: { audio: DimAudio }) {
  const update = useUpdateDimAudio()
  const remove = useDeleteDimAudio()
  const [editing, setEditing] = useState(false)
  const [title, setTitle] = useState(audio.title)
  const [from, setFrom] = useState(String(audio.questionFrom))
  const [to, setTo] = useState(String(audio.questionTo))
  const range = parseRange(from, to)

  return (
    <article className="rounded-lg border border-slate-200 p-3">
      {editing ? (
        <div className="flex flex-wrap items-end gap-2">
          <label className="flex min-w-48 flex-1 flex-col gap-1 font-medium text-slate-700">Ad
            <input className={inputClass} value={title} maxLength={200} onChange={e => setTitle(e.target.value)} />
          </label>
          <input className={numberClass} inputMode="numeric" value={from} aria-label="İlk sual nömrəsi" onChange={e => setFrom(e.target.value)} />
          <input className={numberClass} inputMode="numeric" value={to} aria-label="Son sual nömrəsi" onChange={e => setTo(e.target.value)} />
          <Button className="h-10" loading={update.isPending} disabled={!range || !title.trim()}
            onClick={() => range && update.mutate({ id: audio.id, title: title.trim(), questionFrom: range.from, questionTo: range.to }, { onSuccess: () => setEditing(false) })}>
            Saxla
          </Button>
          <Button variant="ghost" className="h-10" onClick={() => setEditing(false)}>Ləğv et</Button>
        </div>
      ) : (
        <div className="flex flex-wrap items-center gap-2">
          <Headphones className="size-4 text-indigo-600" aria-hidden />
          <span className="font-medium text-slate-800">{audio.title}</span>
          <span className="text-slate-500 tabular-nums">suallar {audio.questionFrom}–{audio.questionTo} · {(audio.sizeBytes / 1024 / 1024).toFixed(1)} MB</span>
          <span className="ml-auto flex gap-1">
            <button type="button" onClick={() => setEditing(true)} className="inline-flex items-center gap-1 rounded-md px-2 py-1 text-xs font-medium text-indigo-700 hover:bg-indigo-50">
              <Pencil className="size-3.5" aria-hidden />Düzəlt
            </button>
            <button type="button" disabled={remove.isPending}
              onClick={() => { if (window.confirm(`"${audio.title}" silinsin? Başlanmış imtahanlarda da çalınmayacaq.`)) remove.mutate(audio.id) }}
              className="inline-flex items-center gap-1 rounded-md px-2 py-1 text-xs font-medium text-rose-700 hover:bg-rose-50">
              <Trash2 className="size-3.5" aria-hidden />Sil
            </button>
          </span>
        </div>
      )}
      <audio controls preload="none" src={dimAudioAdminUrl(audio.id)} className="mt-2 w-full" aria-label={`${audio.title} — dinlə`} />
      {(update.isError || remove.isError) && <div className="mt-2"><Alert title="Alınmadı">{(update.error ?? remove.error)?.message}</Alert></div>}
    </article>
  )
}

/** Topics for many bank questions at once, by number ranges ("1-5: Fonetika"). */
export function DimTopicRanges({ documentId }: { documentId: string }) {
  const setTopics = useSetDimTopics()
  const [text, setText] = useState('')
  return (
    <details className="mt-4 border-t border-slate-100 pt-3 text-sm">
      <summary className="flex cursor-pointer items-center gap-2 font-medium text-slate-700">
        <Tags className="size-4 text-indigo-600" aria-hidden />Mövzuları aralıqla təyin et
      </summary>
      <p className="mt-2 text-slate-600" id={`dim-topics-hint-${documentId}`}>
        Hər sətirdə bir aralıq: <code>1-5: Fonetika</code>, <code>6-10: Leksika</code>. Sonrakı sətir əvvəlkini dəqiqləşdirə bilər
        (<code>1-20: Cəbr</code>, sonra <code>5: Faiz</code>). Mövzu yalnız bankdakı suallara yazılır; nəticədə zəif mövzular buna görə göstərilir.
      </p>
      <textarea value={text} onChange={e => setText(e.target.value)} rows={4} aria-label="Mövzu aralıqları" aria-describedby={`dim-topics-hint-${documentId}`}
        className="mt-2 w-full rounded-lg border border-slate-300 bg-white p-3 font-mono text-sm focus:border-indigo-500 focus:ring-2 focus:ring-indigo-500/20 focus:outline-none" />
      <div className="mt-2 flex flex-wrap items-center gap-3">
        <Button variant="secondary" loading={setTopics.isPending} disabled={text.trim().length < 2}
          onClick={() => setTopics.mutate({ documentId, text })}>Mövzuları tətbiq et</Button>
        {setTopics.data && (
          <p role="status" className="text-slate-600">
            {setTopics.data.updated} suala mövzu yazıldı.
            {setTopics.data.notInBank.length > 0 && ` Bankda olmayan: ${setTopics.data.notInBank.join(', ')}.`}
            {setTopics.data.unread.length > 0 && ` Oxunmayan sətirlər: ${setTopics.data.unread.join(' · ')}.`}
          </p>
        )}
      </div>
      {setTopics.isError && <div className="mt-2"><Alert title="Mövzular tətbiq edilmədi">{setTopics.error.message}</Alert></div>}
    </details>
  )
}

function parseRange(from: string, to: string): { from: number; to: number } | null {
  const a = Number(from.trim())
  const b = to.trim() === '' ? a : Number(to.trim())
  return Number.isInteger(a) && Number.isInteger(b) && a >= 1 && b >= a && b <= 10_000 ? { from: a, to: b } : null
}
