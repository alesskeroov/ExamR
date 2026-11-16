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
import { useTranslation } from 'react-i18next'
import i18n from '../../lib/i18n'


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
  if (!course.examDate) return i18n.t('courses.noDate')
  if (course.daysLeft === null) return i18n.t('courses.datePassed')
  if (course.daysLeft === 0) return i18n.t('courses.examIsToday')
  return i18n.t('courses.daysLeftLong', { count: course.daysLeft })
}

const percent = (part: number, total: number) => (total > 0 ? Math.round((part / total) * 100) : 0)

/** Preparation panel of one subject: readiness, today's three tasks, weak lecture parts and the progress of every file. */
export function CoursePanel({ courseId, onOpenMaterial, onDeleted }: Props) {
  const { t } = useTranslation()
  const course = useCourse(courseId)
  const plan = useCoursePlan(courseId)
  const remove = useDeleteCourse()
  const confirm = useConfirm()
  const navigate = useNavigate()
  const [editing, setEditing] = useState(false)
  const [files, setFiles] = useState(false)

  if (course.isLoading) return <Frame><Skeleton label={t('courses.loading')} /></Frame>
  if (course.isError || !course.data) return <Frame><Alert title={t('courses.notFound')}>{course.error?.message ?? t('courses.maybeDeleted')}</Alert></Frame>
  const data = course.data
  const empty = data.documents.length === 0

  const run = (task: CourseTask) => {
    if (!task.documentId) return
    if (task.kind === 'Mistakes') navigate(`/exams/new?document=${task.documentId}&pool=mistakes`)
    else if (task.kind === 'Test') navigate(`/exams/new?document=${task.documentId}`)
    else if (task.kind === 'Read') onOpenMaterial(task.documentId, task.chatId, { at: task.chunkOrdinal ?? undefined })
    else if (task.kind === 'Cards') {
      const material = plan.data?.materials.find(m => m.documentId === task.documentId)
      const scope = (material?.due ?? 0) > 0 ? 'due' : (material?.weak ?? 0) > 0 ? 'weak' : undefined
      onOpenMaterial(task.documentId, task.chatId, { tab: 'learn', scope })
    }
  }

  const deleteCourse = async () => {
    const ok = await confirm({
      title: t('courses.deleteTitle'), confirmLabel: t('common.delete'), danger: true,
      description: t('courses.deleteText', { name: data.name }),
    })
    if (ok) remove.mutate(courseId, { onSuccess: onDeleted })
  }

  return (
    <Frame>
      <section aria-label={t('dim.report.subject')} className="tutor-border rounded-3xl border bg-white p-5 sm:p-6">
        <div className="flex items-start gap-3">
          <div className="min-w-0 flex-1">
            <h1 className="text-xl font-bold tracking-tight text-slate-900">{data.name}</h1>
            <p className="mt-1 flex items-center gap-1.5 text-sm text-slate-600"><CalendarDays className="size-4" aria-hidden />{daysLabel(data)}</p>
          </div>
          <button type="button" onClick={() => setEditing(true)} aria-label={t('courses.edit')} className="grid size-9 place-items-center rounded-lg text-slate-500 hover:bg-slate-100"><Pencil className="size-4" /></button>
          <button type="button" onClick={deleteCourse} aria-label={t('courses.delete')} className="grid size-9 place-items-center rounded-lg text-slate-500 hover:bg-rose-50 hover:text-rose-600"><Trash2 className="size-4" /></button>
        </div>
        {plan.data && !empty && <>
          <div className="mt-4 flex items-end justify-between gap-4">
            <p className="text-sm text-slate-500">{t('courses.readiness')}</p>
            <p className="text-3xl font-bold tracking-tight text-slate-900 tabular-nums">{plan.data.readiness}%</p>
          </div>
          <div className="mt-2 h-2.5 overflow-hidden rounded-full bg-slate-100" role="progressbar" aria-valuemin={0} aria-valuemax={100} aria-valuenow={plan.data.readiness} aria-label={t('courses.readinessAria')}>
            <div className="h-full rounded-full bg-emerald-500 transition-[width]" style={{ width: `${plan.data.readiness}%` }} />
          </div>
          <p className="mt-2 text-xs text-slate-500">{t('courses.readinessHint')}</p>
        </>}
        <button type="button" onClick={() => setFiles(true)} className="tutor-shared-action mt-4 w-full">
          <FileText className="size-4" aria-hidden />{empty ? t('courses.addFiles') : t('courses.manageFiles')}
        </button>
      </section>

      {remove.isError && <Alert title={t('courses.deleteFailed')}>{remove.error.message}</Alert>}
      {empty ? (
        <EmptyState icon={<Target className="size-8" aria-hidden />} title={t('courses.empty')}
          description={t('courses.emptyText')} />
      ) : plan.isLoading ? <Skeleton label={t('courses.planLoading')} /> : plan.isError ? (
        <Alert title={t('courses.planFailed')} action={<button type="button" onClick={() => plan.refetch()} className="text-sm font-semibold underline">{t('common.retry')}</button>}>{plan.error.message}</Alert>
      ) : plan.data && <>
        <section aria-labelledby="today-title" className="flex flex-col gap-2">
          <h2 id="today-title" className="text-base font-semibold">{t('courses.today')}</h2>
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
            <h2 id="weak-title" className="text-base font-semibold">{t('courses.weakSpots')}</h2>
            <p className="text-xs text-slate-500">{t('courses.weakSpotsHint')}</p>
            <ul className="tutor-border overflow-hidden rounded-2xl border bg-white">
              {plan.data.weakSpots.map((spot, i) => (
                <li key={`${spot.materialDocumentId}-${spot.chunkOrdinal}`} className={cn(i > 0 && 'tutor-border border-t')}>
                  <button type="button" onClick={() => onOpenMaterial(spot.materialDocumentId, spot.chatId, { at: spot.chunkOrdinal, page: spot.pageFrom })}
                    className="flex w-full items-center gap-3 px-4 py-3 text-left hover:bg-slate-50">
                    <span className="min-w-0 flex-1">
                      <span className="block truncate text-sm font-medium text-slate-900">{spot.section ?? spot.materialName}</span>
                      <span className="block truncate text-xs text-slate-500">{[spot.materialName, placeLabel(spot.format, spot.pageFrom, spot.pageTo)].filter(Boolean).join(' · ')}</span>
                    </span>
                    <span className="shrink-0 rounded-full bg-rose-50 px-2 py-0.5 text-xs font-semibold text-rose-700 tabular-nums">{spot.wrongAnswers} {t('dashboard.wrongWord')}</span>
                  </button>
                </li>
              ))}
            </ul>
          </section>
        )}

        {plan.data.materials.length > 0 && (
          <section aria-labelledby="materials-title" className="flex flex-col gap-2">
            <h2 id="materials-title" className="text-base font-semibold">{t('courses.lectures')}</h2>
            <ul className="tutor-border overflow-hidden rounded-2xl border bg-white">
              {plan.data.materials.map((m, i) => (
                <li key={m.documentId} className={cn(i > 0 && 'tutor-border border-t')}>
                  <button type="button" onClick={() => onOpenMaterial(m.documentId, m.chatId, {})} className="flex w-full items-center gap-3 px-4 py-3 text-left hover:bg-slate-50">
                    <span className="min-w-0 flex-1">
                      <span className="block truncate text-sm font-medium text-slate-900">{m.name}</span>
                      <span className="block text-xs text-slate-500">
                        {!m.ready ? t('courses.notReady') : m.cards === 0 ? t('courses.noAutoCards')
                          : `${t('courses.masteredCards', { done: m.mastered, total: m.cards })}${m.due > 0 ? ` · ${t('courses.dueToday', { n: m.due })}` : ''}${m.weak > 0 ? ` · ${t('tutor.topics.weakN', { n: m.weak })}` : ''}`}
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
            <h2 id="banks-title" className="text-base font-semibold">{t('courses.banks')}</h2>
            <ul className="tutor-border overflow-hidden rounded-2xl border bg-white">
              {plan.data.banks.map((b, i) => (
                <li key={b.documentId} className={cn(i > 0 && 'tutor-border border-t')}>
                  <button type="button" onClick={() => navigate(`/exams/new?document=${b.documentId}`)} className="flex w-full items-center gap-3 px-4 py-3 text-left hover:bg-slate-50">
                    <span className="min-w-0 flex-1">
                      <span className="block truncate text-sm font-medium text-slate-900">{b.name}</span>
                      <span className="block text-xs text-slate-500">{t('courses.bankProgress', { answered: b.answered, total: b.questions, correct: b.correct })}{b.wrong > 0 ? ` · ${b.wrong} ${t('dashboard.wrongWord')}` : ''}</span>
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
