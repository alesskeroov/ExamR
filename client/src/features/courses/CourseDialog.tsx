import { useId, useState, type FormEvent } from 'react'
import { Alert } from '../../components/common/Alert'
import { GuideDialog } from '../tutor/GuideDialog'
import { useSaveCourse, type CourseDetail } from './api'

interface Props {
  /** The course to rename / re-date; none = a new course. */
  course?: Pick<CourseDetail, 'id' | 'name' | 'examDate'>
  onClose: () => void
  onSaved?: (course: CourseDetail) => void
}

/** Name and exam date of a subject. */
export function CourseDialog({ course, onClose, onSaved }: Props) {
  const [name, setName] = useState(course?.name ?? '')
  const [examDate, setExamDate] = useState(course?.examDate ?? '')
  const save = useSaveCourse()
  const nameId = useId()
  const dateId = useId()
  const valid = name.trim().length >= 2

  const submit = (event: FormEvent) => {
    event.preventDefault()
    if (!valid || save.isPending) return
    save.mutate({ id: course?.id, name: name.trim(), examDate: examDate || null }, { onSuccess: saved => { onSaved?.(saved); onClose() } })
  }

  return (
    <GuideDialog title={course ? 'Fənni redaktə et' : 'Yeni fənn'} onClose={onClose}>
      <form onSubmit={submit} className="flex flex-col gap-4 px-4 py-4 sm:px-5">
        <div className="flex flex-col gap-1.5">
          <label htmlFor={nameId} className="text-sm font-semibold text-slate-700">Fənnin adı</label>
          <input id={nameId} value={name} onChange={e => setName(e.target.value)} maxLength={100} required autoFocus placeholder="Məsələn: Makroiqtisadiyyat"
            className="tutor-border h-11 rounded-xl border bg-white px-3 text-base text-slate-900 outline-none focus:border-indigo-400 sm:text-sm" />
        </div>
        <div className="flex flex-col gap-1.5">
          <label htmlFor={dateId} className="text-sm font-semibold text-slate-700">İmtahan tarixi <span className="font-normal text-slate-500">(istəyə görə)</span></label>
          <input id={dateId} type="date" value={examDate} onChange={e => setExamDate(e.target.value)}
            className="tutor-border h-11 rounded-xl border bg-white px-3 text-base text-slate-900 outline-none focus:border-indigo-400 sm:text-sm" />
          <p className="text-xs text-slate-500">Tarixə görə gündəlik iş həcmi hesablanır.</p>
        </div>
        {save.isError && <Alert title="Yadda saxlanmadı">{save.error.message}</Alert>}
        <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
          <button type="button" onClick={onClose} className="tutor-shared-action sm:px-5">Ləğv et</button>
          <button type="submit" disabled={!valid || save.isPending} className="btn-primary inline-flex h-11 items-center justify-center rounded-xl px-5 text-sm font-semibold disabled:opacity-60">
            {save.isPending ? 'Saxlanılır…' : 'Yadda saxla'}
          </button>
        </div>
      </form>
    </GuideDialog>
  )
}
