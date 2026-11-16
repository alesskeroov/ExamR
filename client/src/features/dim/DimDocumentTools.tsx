import { Headphones, Pencil, Tags, Trash2, Upload } from 'lucide-react'
import { useRef, useState } from 'react'
import { Alert } from '../../components/common/Alert'
import { Button } from '../../components/common/Button'
import {
  DIM_AUDIO_DEFAULT_PLAYS, DIM_AUDIO_MAX_BYTES, dimAudioAdminUrl, useDeleteDimAudio, useDimAudios, useSetDimTopics, useUpdateDimAudio, useUploadDimAudio,
  type DimAudio,
} from './api'
import { inputClass } from './ui'
import { Trans, useTranslation } from 'react-i18next'

const numberClass = inputClass.replace('w-full', 'w-20')

/**
 * Listening recordings of a document (e.g. the English section): each covers a range of question numbers, and the
 * student can play it on those questions during the exam.
 */
export function DimAudioPanel({ documentId }: { documentId: string }) {
  const { t } = useTranslation()
  const audios = useDimAudios(documentId)
  const upload = useUploadDimAudio()
  const fileRef = useRef<HTMLInputElement>(null)
  const [file, setFile] = useState<File | null>(null)
  const [title, setTitle] = useState('')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [plays, setPlays] = useState(String(DIM_AUDIO_DEFAULT_PLAYS))
  const [clientError, setClientError] = useState<string | null>(null)
  const range = parseRange(from, to)
  const maxPlays = parsePlays(plays)

  function pick(next: File | null) {
    setClientError(null)
    if (next && next.size > DIM_AUDIO_MAX_BYTES) {
      setClientError(t('dim.audio.tooLarge'))
      next = null
    }
    setFile(next)
    if (next && !title) setTitle(next.name.replace(/\.[^.]+$/, ''))
  }

  function submit() {
    if (!file || !range || maxPlays === undefined) return
    upload.mutate({ documentId, file, title: title.trim(), questionFrom: range.from, questionTo: range.to, maxPlays }, {
      onSuccess: () => {
        setFile(null); setTitle(''); setFrom(''); setTo('')
        if (fileRef.current) fileRef.current.value = ''
      },
    })
  }

  return (
    <details className="mt-4 border-t border-slate-100 pt-3 text-sm" open={(audios.data?.length ?? 0) > 0 || undefined}>
      <summary className="flex cursor-pointer items-center gap-2 font-medium text-slate-700">
        <Headphones className="size-4 text-indigo-600" aria-hidden />{t('dim.audio.title')}
        {audios.data && audios.data.length > 0 && <span className="font-normal text-slate-500">— {t('dim.audio.files', { n: audios.data.length })}</span>}
      </summary>
      <p className="mt-2 text-slate-600">
        {t('dim.audio.text')}
      </p>

      {audios.isError && <div className="mt-2"><Alert title={t('dim.audio.loadFailed')}>{audios.error.message}</Alert></div>}
      {audios.data && audios.data.length > 0 && (
        <ul className="mt-3 flex flex-col gap-2">{audios.data.map(a => <li key={a.id}><AudioRow audio={a} /></li>)}</ul>
      )}

      <div className="mt-3 grid gap-3 rounded-lg border border-dashed border-slate-300 p-3 sm:grid-cols-[minmax(0,1fr)_minmax(0,1fr)_auto]">
        <label className="flex flex-col gap-1.5 font-medium text-slate-700">{t('dim.audio.file')}
          <input ref={fileRef} type="file" accept="audio/mpeg,audio/mp4,audio/x-m4a,audio/ogg,audio/wav,.mp3,.m4a,.ogg,.wav"
            onChange={e => pick(e.target.files?.[0] ?? null)}
            className="text-sm file:mr-3 file:rounded-md file:border-0 file:bg-indigo-50 file:px-3 file:py-2 file:font-medium file:text-indigo-700" />
        </label>
        <label className="flex flex-col gap-1.5 font-medium text-slate-700">{t('dim.admin.name')}
          <input className={inputClass} value={title} maxLength={200} placeholder={t('dim.audio.namePlaceholder')} onChange={e => setTitle(e.target.value)} />
        </label>
        <fieldset className="flex flex-col gap-1.5">
          <legend className="mb-1.5 font-medium text-slate-700">{t('exam.session.questions')}</legend>
          <div className="flex items-center gap-2">
            <input className={numberClass} inputMode="numeric" value={from} placeholder="1" aria-label={t('dim.audio.firstNumber')} onChange={e => setFrom(e.target.value)} />
            <span aria-hidden>–</span>
            <input className={numberClass} inputMode="numeric" value={to} placeholder="5" aria-label={t('dim.audio.lastNumber')} onChange={e => setTo(e.target.value)} />
          </div>
        </fieldset>
        <div className="flex flex-wrap items-center gap-3 sm:col-span-3">
          <label className="flex items-center gap-2 font-medium text-slate-700">{t('dim.audio.playLimit')}
            <input className={numberClass} inputMode="numeric" value={plays} placeholder={t('dim.admin.unlimited')} aria-describedby={`dim-plays-hint-${documentId}`}
              onChange={e => setPlays(e.target.value)} />
          </label>
          <span id={`dim-plays-hint-${documentId}`} className="text-xs text-slate-500">{t('dim.audio.playHint')}</span>
          <Button variant="secondary" loading={upload.isPending} disabled={!file || !range || maxPlays === undefined} onClick={submit}>
            <Upload className="size-4" aria-hidden />{t('dim.audio.upload')}
          </Button>
          {upload.isPending && <span role="status" className="text-slate-500">{t('common.loadingDots')}</span>}
        </div>
      </div>
      {(clientError || upload.isError) && <div className="mt-2"><Alert title={t('dim.audio.uploadFailed')}>{clientError ?? upload.error?.message}</Alert></div>}
    </details>
  )
}

function AudioRow({ audio }: { audio: DimAudio }) {
  const { t } = useTranslation()
  const update = useUpdateDimAudio()
  const remove = useDeleteDimAudio()
  const [editing, setEditing] = useState(false)
  const [title, setTitle] = useState(audio.title)
  const [from, setFrom] = useState(String(audio.questionFrom))
  const [to, setTo] = useState(String(audio.questionTo))
  const [plays, setPlays] = useState(audio.maxPlays === null ? '' : String(audio.maxPlays))
  const range = parseRange(from, to)
  const maxPlays = parsePlays(plays)

  return (
    <article className="rounded-lg border border-slate-200 p-3">
      {editing ? (
        <div className="flex flex-wrap items-end gap-2">
          <label className="flex min-w-48 flex-1 flex-col gap-1 font-medium text-slate-700">{t('dim.admin.name')}
            <input className={inputClass} value={title} maxLength={200} onChange={e => setTitle(e.target.value)} />
          </label>
          <input className={numberClass} inputMode="numeric" value={from} aria-label={t('dim.audio.firstNumber')} onChange={e => setFrom(e.target.value)} />
          <input className={numberClass} inputMode="numeric" value={to} aria-label={t('dim.audio.lastNumber')} onChange={e => setTo(e.target.value)} />
          <label className="flex flex-col gap-1 font-medium text-slate-700">{t('dim.audio.limit')}
            <input className={numberClass} inputMode="numeric" value={plays} placeholder={t('dim.admin.unlimited')} onChange={e => setPlays(e.target.value)} />
          </label>
          <Button className="h-10" loading={update.isPending} disabled={!range || !title.trim() || maxPlays === undefined}
            onClick={() => range && maxPlays !== undefined && update.mutate({ id: audio.id, title: title.trim(), questionFrom: range.from, questionTo: range.to, maxPlays },
              { onSuccess: () => setEditing(false) })}>
            {t('common.save')}
          </Button>
          <Button variant="ghost" className="h-10" onClick={() => setEditing(false)}>{t('common.cancel')}</Button>
        </div>
      ) : (
        <div className="flex flex-wrap items-center gap-2">
          <Headphones className="size-4 text-indigo-600" aria-hidden />
          <span className="font-medium text-slate-800">{audio.title}</span>
          <span className="text-slate-500 tabular-nums">
            {t('exam.result.questionsRange', { range: `${audio.questionFrom}–${audio.questionTo}` })} · {audio.maxPlays === null ? t('dim.audio.unlimitedPlays') : t('dim.audio.plays', { n: audio.maxPlays })} · {(audio.sizeBytes / 1024 / 1024).toFixed(1)} MB
          </span>
          <span className="ml-auto flex gap-1">
            <button type="button" onClick={() => setEditing(true)} className="inline-flex items-center gap-1 rounded-md px-2 py-1 text-xs font-medium text-indigo-700 hover:bg-indigo-50">
              <Pencil className="size-3.5" aria-hidden />{t('dim.audio.edit')}
            </button>
            <button type="button" disabled={remove.isPending}
              onClick={() => { if (window.confirm(t('dim.audio.deleteConfirm', { title: audio.title }))) remove.mutate(audio.id) }}
              className="inline-flex items-center gap-1 rounded-md px-2 py-1 text-xs font-medium text-rose-700 hover:bg-rose-50">
              <Trash2 className="size-3.5" aria-hidden />{t('common.delete')}
            </button>
          </span>
        </div>
      )}
      <audio controls preload="none" src={dimAudioAdminUrl(audio.id)} className="mt-2 w-full" aria-label={t('dim.audio.listenAria', { title: audio.title })} />
      {(update.isError || remove.isError) && <div className="mt-2"><Alert title={t('profile.actionFailed')}>{(update.error ?? remove.error)?.message}</Alert></div>}
    </article>
  )
}

/** Topics for many bank questions at once, by number ranges ("1-5: Fonetika"). */
export function DimTopicRanges({ documentId }: { documentId: string }) {
  const { t } = useTranslation()
  const setTopics = useSetDimTopics()
  const [text, setText] = useState('')
  return (
    <details className="mt-4 border-t border-slate-100 pt-3 text-sm">
      <summary className="flex cursor-pointer items-center gap-2 font-medium text-slate-700">
        <Tags className="size-4 text-indigo-600" aria-hidden />{t('dim.topics.title')}
      </summary>
      <p className="mt-2 text-slate-600" id={`dim-topics-hint-${documentId}`}>
        <Trans i18nKey="dim.topics.hint" components={{ c: <code /> }} />
      </p>
      <textarea value={text} onChange={e => setText(e.target.value)} rows={4} aria-label={t('dim.topics.aria')} aria-describedby={`dim-topics-hint-${documentId}`}
        className="mt-2 w-full rounded-lg border border-slate-300 bg-white p-3 font-mono text-sm focus:border-indigo-500 focus:ring-2 focus:ring-indigo-500/20 focus:outline-none" />
      <div className="mt-2 flex flex-wrap items-center gap-3">
        <Button variant="secondary" loading={setTopics.isPending} disabled={text.trim().length < 2}
          onClick={() => setTopics.mutate({ documentId, text })}>{t('dim.topics.apply')}</Button>
        {setTopics.data && (
          <p role="status" className="text-slate-600">
            {t('dim.topics.updated', { n: setTopics.data.updated })}
            {setTopics.data.notInBank.length > 0 && ` ${t('dim.topics.notInBank', { list: setTopics.data.notInBank.join(', ') })}`}
            {setTopics.data.unread.length > 0 && ` ${t('dim.topics.unread', { list: setTopics.data.unread.join(' · ') })}`}
          </p>
        )}
      </div>
      {setTopics.isError && <div className="mt-2"><Alert title={t('dim.topics.failed')}>{setTopics.error.message}</Alert></div>}
    </details>
  )
}

function parseRange(from: string, to: string): { from: number; to: number } | null {
  const a = Number(from.trim())
  const b = to.trim() === '' ? a : Number(to.trim())
  return Number.isInteger(a) && Number.isInteger(b) && a >= 1 && b >= a && b <= 10_000 ? { from: a, to: b } : null
}

/** Play limit field: empty = no limit (null); undefined = invalid. */
function parsePlays(value: string): number | null | undefined {
  if (value.trim() === '') return null
  const n = Number(value.trim())
  return Number.isInteger(n) && n >= 1 && n <= 10 ? n : undefined
}
