import { BookOpen, CalendarDays, CheckCircle2, FileText, Layers, ListChecks, Pencil, RotateCcw, Target, Trash2, type LucideIcon } from 'lucide-react'
import { useState } from 'react'
import { useNavigate } from 'react-router'
import { Alert } from '../../components/common/Alert'
import { EmptyState } from '../../components/common/EmptyState'
import { useConfirm } from '../../lib/use-confirm'
import { cn } from '../../lib/utils'
import { placeLabel } from '../tutor/place-label'
import { Frame, Skeleton } from '../tutor/TutorShared'
import { useCourse, useCoursePlan, useDeleteCourse, type CourseDetail, type CourseTask, type CourseTaskKind, type GuideTarget } from './api'
import { CourseDialog } from './CourseDialog'
import { CourseFilesDialog } from './CourseFilesDialog'


interface Props {
  courseId: string
  onOpenMaterial: (documentId: string, chatId: string | null, target: GuideTarget) => void
  onDeleted: () => void
}

const taskIcon: Record<CourseTaskKind, LucideIcon> = { Mistakes: RotateCcw, Read: BookOpen, Cards: Layers, Test: ListChecks, Done: CheckCircle2 }
const taskTone: Record<CourseTaskKind, string> = {
  Mistakes: 'bg-rose-50 text-rose-700', Read: 'bg-indigo-50 text-indigo-700', Cards: 'bg-amber-50 text-amber-800',
  Test: 'bg-emerald-50 text-emerald-700', Done: 'bg-emerald-50 text-emerald-700',
}

function daysLabel(course: CourseDetail) {
  if (!course.examDate) return 'İmtahan tarixi təyin edilməyib'
  if (course.daysLeft === null) return 'İmtahan tarixi keçib'
  if (course.daysLeft === 0) return 'İmtahan bu gündür'
  return `İmtahana ${course.daysLeft} gün qalıb`
}

const percent = (part: number, total: number) => (total > 0 ? Math.round((part / total) * 100) : 0)

/** Preparation panel of one subject: readiness, today's three tasks, weak lecture parts and the progress of every file. */
export function CoursePanel({ courseId, onOpenMaterial, onDeleted }: Props) {
  const course = useCourse(courseId)
  const plan = useCoursePlan(courseId)
  const remove = useDeleteCourse()
  const confirm = useConfirm()
  const navigate = useNavigate()
  const [editing, setEditing] = useState(false)
  const [files, setFiles] = useState(false)

  if (course.isLoading) return <Frame><Skeleton label="Fənn yüklənir…" /></Frame>
  if (course.isError || !course.data) return <Frame><Alert title="Fənn tapılmadı">{course.error?.message ?? 'Silinmiş ola bilər.'}</Alert></Frame>
  const data = course.data
  const empty = data.documents.length === 0

  const run = (task: CourseTask) => {
    if (!task.documentId) return
    if (task.kind === 'Mistakes') navigate(`/exams/new?document=${task.documentId}&pool=mistakes`)
    else if (task.kind === 'Test') navigate(`/exams/new?document=${task.documentId}`)
    else if (task.kind === 'Read') onOpenMaterial(task.documentId, task.chatId, { at: task.chunkOrdinal ?? undefined })
    else if (task.kind === 'Cards') {
      const weak = plan.data?.materials.find(m => m.documentId === task.documentId)?.weak ?? 0
      onOpenMaterial(task.documentId, task.chatId, { tab: 'learn', scope: weak > 0 ? 'weak' : undefined })
    }
  }

  const deleteCourse = async () => {
    const ok = await confirm({
      title: 'Fənn silinsin?', confirmLabel: 'Sil', danger: true,
      description: `"${data.name}" fənni silinəcək. Fayllar, kartlar və imtahan nəticələri qalır.`,
    })
    if (ok) remove.mutate(courseId, { onSuccess: onDeleted })
  }

  return (
    <Frame>
      <section aria-label="Fənn" className="tutor-border rounded-3xl border bg-white p-5 sm:p-6">
        <div className="flex items-start gap-3">
          <div className="min-w-0 flex-1">
            <h1 className="text-xl font-bold tracking-tight text-slate-900">{data.name}</h1>
            <p className="mt-1 flex items-center gap-1.5 text-sm text-slate-600"><CalendarDays className="size-4" aria-hidden />{daysLabel(data)}</p>
          </div>
          <button type="button" onClick={() => setEditing(true)} aria-label="Fənni redaktə et" className="grid size-9 place-items-center rounded-lg text-slate-500 hover:bg-slate-100"><Pencil className="size-4" /></button>
          <button type="button" onClick={deleteCourse} aria-label="Fənni sil" className="grid size-9 place-items-center rounded-lg text-slate-500 hover:bg-rose-50 hover:text-rose-600"><Trash2 className="size-4" /></button>
        </div>
        {plan.data && !empty && <>
          <div className="mt-4 flex items-end justify-between gap-4">
            <p className="text-sm text-slate-500">Hazırlıq</p>
            <p className="text-3xl font-bold tracking-tight text-slate-900 tabular-nums">{plan.data.readiness}%</p>
          </div>
          <div className="mt-2 h-2.5 overflow-hidden rounded-full bg-slate-100" role="progressbar" aria-valuemin={0} aria-valuemax={100} aria-valuenow={plan.data.readiness} aria-label="Hazırlıq faizi">
            <div className="h-full rounded-full bg-emerald-500 transition-[width]" style={{ width: `${plan.data.readiness}%` }} />
          </div>
          <p className="mt-2 text-xs text-slate-500">Mənimsənilmiş kartlar və sual bankında son dəfə düz cavablanan suallar əsasında.</p>
        </>}
        <button type="button" onClick={() => setFiles(true)} className="tutor-shared-action mt-4 w-full">
          <FileText className="size-4" aria-hidden />{empty ? 'Mühazirə və sual bankı əlavə et' : 'Faylları idarə et'}
        </button>
      </section>

      {remove.isError && <Alert title="Fənn silinmədi">{remove.error.message}</Alert>}
      {empty ? (
        <EmptyState icon={<Target className="size-8" aria-hidden />} title="Fənn hələ boşdur"
          description="Mühazirə fayllarını və imtahan sual bankını əlavə edin — hazırlıq planı və zəif mövzular avtomatik hesablanacaq." />
      ) : plan.isLoading ? <Skeleton label="Plan hazırlanır…" /> : plan.isError ? (
        <Alert title="Plan hazırlanmadı" action={<button type="button" onClick={() => plan.refetch()} className="text-sm font-semibold underline">Yenidən cəhd et</button>}>{plan.error.message}</Alert>
      ) : plan.data && <>
        <section aria-labelledby="today-title" className="flex flex-col gap-2">
          <h2 id="today-title" className="text-base font-semibold">Bu gün</h2>
          <ol className="flex flex-col gap-2">
            {plan.data.today.map((task, i) => {
              const Icon = taskIcon[task.kind]
              return (
                <li key={i}>
                  <button type="button" disabled={!task.documentId} onClick={() => run(task)}
                    className="tutor-border flex w-full items-center gap-3 rounded-2xl border bg-white px-4 py-3 text-left enabled:hover:bg-slate-50">
                    <span className={cn('grid size-9 shrink-0 place-items-center rounded-full', taskTone[task.kind])}><Icon className="size-4" aria-hidden /></span>
                    <span className="min-w-0 flex-1">
                      <span className="block text-[15px] font-semibold text-slate-900">{task.title}</span>
                      <span className="block text-sm text-slate-600">{task.detail}</span>
                    </span>
                  </button>
                </li>
              )
            })}
          </ol>
        </section>

        {plan.data.weakSpots.length > 0 && (
          <section aria-labelledby="weak-title" className="flex flex-col gap-2">
            <h2 id="weak-title" className="text-base font-semibold">Zəif hissələr</h2>
            <p className="text-xs text-slate-500">Səhv cavab verdiyiniz sualların mühazirədə ən çox uyğun gəldiyi yerlər.</p>
            <ul className="tutor-border overflow-hidden rounded-2xl border bg-white">
              {plan.data.weakSpots.map((spot, i) => (
                <li key={`${spot.materialDocumentId}-${spot.chunkOrdinal}`} className={cn(i > 0 && 'tutor-border border-t')}>
                  <button type="button" onClick={() => onOpenMaterial(spot.materialDocumentId, spot.chatId, { at: spot.chunkOrdinal, page: spot.pageFrom })}
                    className="flex w-full items-center gap-3 px-4 py-3 text-left hover:bg-slate-50">
                    <span className="min-w-0 flex-1">
                      <span className="block truncate text-sm font-medium text-slate-900">{spot.section ?? spot.materialName}</span>
                      <span className="block truncate text-xs text-slate-500">{[spot.materialName, placeLabel(spot.format, spot.pageFrom, spot.pageTo)].filter(Boolean).join(' · ')}</span>
                    </span>
                    <span className="shrink-0 rounded-full bg-rose-50 px-2 py-0.5 text-xs font-semibold text-rose-700 tabular-nums">{spot.wrongAnswers} səhv</span>
                  </button>
                </li>
              ))}
            </ul>
          </section>
        )}

        {plan.data.materials.length > 0 && (
          <section aria-labelledby="materials-title" className="flex flex-col gap-2">
            <h2 id="materials-title" className="text-base font-semibold">Mühazirələr</h2>
            <ul className="tutor-border overflow-hidden rounded-2xl border bg-white">
              {plan.data.materials.map((m, i) => (
                <li key={m.documentId} className={cn(i > 0 && 'tutor-border border-t')}>
                  <button type="button" onClick={() => onOpenMaterial(m.documentId, m.chatId, {})} className="flex w-full items-center gap-3 px-4 py-3 text-left hover:bg-slate-50">
                    <span className="min-w-0 flex-1">
                      <span className="block truncate text-sm font-medium text-slate-900">{m.name}</span>
                      <span className="block text-xs text-slate-500">
                        {!m.ready ? 'Hələ hazır deyil — açın ki, emal olunsun' : m.cards === 0 ? 'Avtomatik kart yoxdur — konspektdən öz kartını yarat'
                          : `${m.mastered}/${m.cards} kart mənimsənilib${m.weak > 0 ? ` · ${m.weak} zəif` : ''}`}
                      </span>
                    </span>
                    {m.cards > 0 && <span className="shrink-0 text-sm font-semibold text-slate-700 tabular-nums">{percent(m.mastered, m.cards)}%</span>}
                  </button>
                </li>
              ))}
            </ul>
          </section>
        )}

        {plan.data.banks.length > 0 && (
          <section aria-labelledby="banks-title" className="flex flex-col gap-2">
            <h2 id="banks-title" className="text-base font-semibold">Sual bankları</h2>
            <ul className="tutor-border overflow-hidden rounded-2xl border bg-white">
              {plan.data.banks.map((b, i) => (
                <li key={b.documentId} className={cn(i > 0 && 'tutor-border border-t')}>
                  <button type="button" onClick={() => navigate(`/exams/new?document=${b.documentId}`)} className="flex w-full items-center gap-3 px-4 py-3 text-left hover:bg-slate-50">
                    <span className="min-w-0 flex-1">
                      <span className="block truncate text-sm font-medium text-slate-900">{b.name}</span>
                      <span className="block text-xs text-slate-500">{b.answered}/{b.questions} sual həll edilib · {b.correct} düz{b.wrong > 0 ? ` · ${b.wrong} səhv` : ''}</span>
                    </span>
                    <span className="shrink-0 text-sm font-semibold text-slate-700 tabular-nums">{percent(b.correct, b.questions)}%</span>
                  </button>
                </li>
              ))}
            </ul>
          </section>
        )}
      </>}

      {editing && <CourseDialog course={data} onClose={() => setEditing(false)} />}
      {files && <CourseFilesDialog course={data} onClose={() => setFiles(false)} />}
    </Frame>
  )
}
