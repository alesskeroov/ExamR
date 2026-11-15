import { FileText } from 'lucide-react'
import { useState } from 'react'
import { Alert } from '../../components/common/Alert'
import { cn } from '../../lib/utils'
import { GuideDialog } from '../tutor/GuideDialog'
import { useCourseCandidates, useSetCourseDocuments, type CourseCandidate, type CourseDetail, type CourseRole } from './api'

const limits: Record<CourseRole, number> = { Material: 10, Bank: 3 }

interface Props {
  course: CourseDetail
  onClose: () => void
}

/** Picks the course's lecture files (guide) and question banks (exam PDFs) from the files the student can read. */
export function CourseFilesDialog({ course, onClose }: Props) {
  const candidates = useCourseCandidates(true)
  const save = useSetCourseDocuments(course.id)
  const [picked, setPicked] = useState<Record<CourseRole, string[]>>(() => ({
    Material: course.documents.filter(d => d.role === 'Material').map(d => d.documentId),
    Bank: course.documents.filter(d => d.role === 'Bank').map(d => d.documentId),
  }))

  const toggle = (role: CourseRole, id: string) => setPicked(current => ({
    ...current,
    [role]: current[role].includes(id) ? current[role].filter(x => x !== id) : [...current[role], id],
  }))

  const section = (role: CourseRole, title: string, hint: string) => {
    const items = (candidates.data ?? []).filter(c => c.role === role)
    const full = picked[role].length >= limits[role]
    return (
      <fieldset className="flex flex-col gap-2">
        <legend className="mb-1 text-sm font-semibold text-slate-700">{title} <span className="font-normal text-slate-500">· {picked[role].length}/{limits[role]}</span></legend>
        <p className="text-xs text-slate-500">{hint}</p>
        {items.length === 0
          ? <p className="tutor-border rounded-xl border border-dashed px-3 py-4 text-center text-sm text-slate-500">Uyğun fayl yoxdur.</p>
          : <ul className="tutor-border overflow-hidden rounded-xl border bg-white">
            {items.map((item: CourseCandidate, i) => {
              const checked = picked[role].includes(item.documentId)
              return (
                <li key={item.documentId} className={cn(i > 0 && 'tutor-border border-t')}>
                  <label className={cn('flex cursor-pointer items-center gap-3 px-3 py-2.5 hover:bg-slate-50', !checked && full && 'cursor-not-allowed opacity-60')}>
                    <input type="checkbox" checked={checked} disabled={!checked && full} onChange={() => toggle(role, item.documentId)} className="size-4 accent-indigo-600" />
                    <span className="min-w-0 flex-1 truncate text-sm">{item.name}</span>
                    <span className="shrink-0 text-[11px] font-semibold text-slate-400 uppercase">{item.format}</span>
                  </label>
                </li>
              )
            })}
          </ul>}
      </fieldset>
    )
  }

  return (
    <GuideDialog title="Fənnin faylları" onClose={onClose} tall>
      <div className="guide-scroll flex min-h-0 flex-1 flex-col gap-5 overflow-y-auto px-4 py-4 sm:px-5">
        {candidates.isLoading && <p role="status" className="text-sm text-slate-500">Fayllar yüklənir…</p>}
        {candidates.isError && <Alert title="Fayllar yüklənmədi">{candidates.error.message}</Alert>}
        {candidates.data && <>
          {section('Material', 'Mühazirə materialları', 'Bələdçidə oxuduğunuz PDF, Word və PowerPoint faylları.')}
          {section('Bank', 'Sual bankları', 'İmtahan və test həll etdiyiniz, variantlı suallardan ibarət fayllar.')}
        </>}
        {save.isError && <Alert title="Yadda saxlanmadı">{save.error.message}</Alert>}
      </div>
      <div className="tutor-border flex shrink-0 flex-col-reverse gap-2 border-t px-4 py-3 sm:flex-row sm:justify-end sm:px-5">
        <button type="button" onClick={onClose} className="tutor-shared-action sm:px-5">Ləğv et</button>
        <button type="button" disabled={save.isPending || !candidates.data} onClick={() => save.mutate({ materials: picked.Material, banks: picked.Bank }, { onSuccess: onClose })}
          className="btn-primary inline-flex h-11 items-center justify-center gap-2 rounded-xl px-5 text-sm font-semibold disabled:opacity-60">
          {save.isPending ? 'Saxlanılır…' : <><FileText className="size-4" aria-hidden />Yadda saxla</>}
        </button>
      </div>
    </GuideDialog>
  )
}
