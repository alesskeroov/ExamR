import { BookOpen, CalendarDays, FileText, GraduationCap, Layers, ListChecks, Menu, MessageSquareText, PanelLeftClose, PanelLeftOpen, PanelRight, Plus, Search, Trash2, X, type LucideIcon } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { useSearchParams } from 'react-router'
import { Alert } from '../../components/common/Alert'
import { useConfirm } from '../../lib/use-confirm'
import { useCourses, type CourseSummary, type GuideTarget } from '../courses/api'
import { CourseDialog } from '../courses/CourseDialog'
import { CoursePanel } from '../courses/CoursePanel'
import { cn } from '../../lib/utils'
import { useChat, useChats, useCreateChat, useDeck, useDeleteChat, useReviewToday, useTutorDocuments, type ChatSession, type ChatSource, type DueDocument, type LearnScope, type ReviewToday, type SearchHit, type StudyCard } from './api'
import { openFeedback } from '../support/feedback-events'
import { GuideCardList } from './GuideCardList'
import { GuideLearn } from './GuideLearn'
import { GuideSearch } from './GuideSearch'
import { GuideTopics } from './GuideTopics'
import { TutorDocumentPicker } from './TutorDocumentPicker'
import { TutorPreview } from './TutorPreview'
import { Frame, Skeleton } from './TutorShared'
import './tutor.css'
import { useTranslation } from 'react-i18next'
import i18n from '../../lib/i18n'

const sidebarKey = 'examr-guide-sidebar'

function readStored(key: string) {
  try { return localStorage.getItem(key) } catch { return null }
}
function store(key: string, value: string) {
  try { localStorage.setItem(key, value) } catch { /* private mode: keep the in-memory choice */ }
}

type Tab = 'topics' | 'learn' | 'cards' | 'search'
const tabs: { id: Tab; label: string; icon: LucideIcon }[] = [
  { id: 'topics', label: 'tutor.tabs.topics', icon: BookOpen },
  { id: 'learn', label: 'tutor.learn.title', icon: Layers },
  { id: 'cards', label: 'tutor.cards.title', icon: ListChecks },
  { id: 'search', label: 'friends.search', icon: Search },
]
const tabFrom = (value: string | null): Tab => value === 'learn' || value === 'cards' || value === 'search' ? value : 'topics'

function scopeFrom(params: URLSearchParams): LearnScope {
  const scope = params.get('scope')
  const topic = params.get('topic')
  if (scope === 'topic' && topic) return { kind: 'topic', key: topic }
  return scope === 'due' || scope === 'weak' || scope === 'mixed' ? { kind: scope } : { kind: 'all' }
}

/**
 * Öyrənmə bələdçisi: the document's own questions as private flashcards, without any AI call.
 * (The former AI Tutor page, TutorPage, is kept in the code but no longer routed.)
 */
export function GuidePage() {
  const { t } = useTranslation()
  const [params, setParams] = useSearchParams()
  const chatId = params.get('chat')
  const tab = tabFrom(params.get('tab'))
  const scope = scopeFrom(params)
  const confirm = useConfirm()
  const chats = useChats()
  const chat = useChat(chatId)
  const documents = useTutorDocuments()
  const createChat = useCreateChat()
  const deleteChat = useDeleteChat()

  const [sidebarOpen, setSidebarOpen] = useState(() => readStored(sidebarKey) !== 'closed')
  const [drawerOpen, setDrawerOpen] = useState(false)
  const [preview, setPreview] = useState<{ open: boolean; source: ChatSource | null }>({ open: false, source: null })
  const workspace = useRef<HTMLDivElement>(null)

  const session = chat.data?.session
  const docInfo = documents.data?.find(d => d.documentId === session?.documentId)
  const format = docInfo?.format ?? session?.documentName.split('.').pop()?.toLowerCase() ?? ''

  const courseId = chatId ? null : params.get('course')
  const courses = useCourses()
  const today = useReviewToday()
  const [newCourse, setNewCourse] = useState(false)

  // Opening a document closes the drawer; a link to a place in it ("at": indexed part, "page") opens the preview there.
  const [shownChat, setShownChat] = useState(chatId)
  if (shownChat !== chatId) {
    setShownChat(chatId)
    setDrawerOpen(false)
    const at = params.get('at')
    const page = params.get('page') ? Number(params.get('page')) : null
    setPreview(at !== null && chatId
      ? { open: true, source: { number: 0, chunkOrdinal: Number(at), pageFrom: page, pageTo: page, section: null, excerpt: '', cited: true } }
      : { open: false, source: null })
  }

  useEffect(() => {
    if (!drawerOpen) return
    const close = (event: KeyboardEvent) => { if (event.key === 'Escape') setDrawerOpen(false) }
    window.addEventListener('keydown', close)
    return () => window.removeEventListener('keydown', close)
  }, [drawerOpen])

  const go = (next: Tab, extra: Record<string, string> = {}) =>
    setParams({ chat: chatId ?? '', ...(next === 'topics' ? {} : { tab: next }), ...extra }, { replace: true })
  const learn = (next: LearnScope) => go('learn', next.kind === 'topic' ? { scope: 'topic', topic: next.key } : next.kind === 'all' ? {} : { scope: next.kind })
  const showSource = (card: StudyCard) => setPreview({
    open: true,
    source: { number: card.source, chunkOrdinal: card.chunkOrdinal, pageFrom: card.page, pageTo: card.page, section: null, excerpt: card.front, cited: true },
  })
  const showHit = (hit: SearchHit) => setPreview({
    open: true,
    source: { number: 0, chunkOrdinal: hit.ordinal, pageFrom: hit.pageFrom, pageTo: hit.pageTo, section: hit.section, excerpt: '', cited: true },
  })

  const toggleSidebar = () => setSidebarOpen(open => { store(sidebarKey, open ? 'closed' : 'open'); return !open })
  // One entry per document: an existing one is reopened with its progress.
  const startDocument = (documentId: string) => {
    const existing = chats.data?.items.find(c => c.documentId === documentId)
    if (existing) setParams({ chat: existing.id })
    else createChat.mutate(documentId, { onSuccess: created => setParams({ chat: created.id }) })
  }
  const remove = async (target: ChatSession) => {
    const ok = await confirm({
      title: t('tutor.guide.removeTitle'), confirmLabel: t('friends.removeConfirm'), danger: true,
      description: t('tutor.guide.removeText', { name: target.documentName }),
    })
    if (!ok) return
    deleteChat.mutate(target.id, { onSuccess: () => { if (target.id === chatId) setParams({}) } })
  }
  const newDocument = () => { setParams({}); setDrawerOpen(false) }

  /** Opens a material of a course (or an exam result's link) in the guide, creating its entry on first use. */
  const openMaterial = (documentId: string, knownChat: string | null, target: GuideTarget) => {
    const extra: Record<string, string> = {
      ...(target.tab ? { tab: target.tab } : {}), ...(target.scope ? { scope: target.scope } : {}),
      ...(target.at != null ? { at: String(target.at) } : {}), ...(target.page != null ? { page: String(target.page) } : {}),
    }
    const existing = knownChat ?? chats.data?.items.find(c => c.documentId === documentId)?.id
    if (existing) setParams({ chat: existing, ...extra })
    else createChat.mutate(documentId, { onSuccess: created => setParams({ chat: created.id, ...extra }, { replace: true }) })
  }

  // "/tutor?document=…&at=…" (from an exam result): open that material once the student's entries are known.
  const linkedDocument = params.get('document')
  const followed = useRef<string | null>(null)
  useEffect(() => {
    if (!linkedDocument || !chats.data || followed.current === linkedDocument) return
    followed.current = linkedDocument
    const at = params.get('at')
    openMaterial(linkedDocument, null, { at: at !== null ? Number(at) : undefined, page: params.get('page') ? Number(params.get('page')) : null })
  })

  const list = <DocumentList items={chats.data?.items ?? []} loading={chats.isLoading} activeId={chatId}
    onSelect={id => setParams({ chat: id })} onNew={newDocument} onDelete={remove}
    courses={courses.data ?? []} activeCourseId={courseId} onSelectCourse={id => { setParams({ course: id }); setDrawerOpen(false) }}
    onNewCourse={() => setNewCourse(true)} today={today.data}
    onOpenDue={doc => { openMaterial(doc.documentId, doc.chatId, { tab: 'learn', scope: 'due' }); setDrawerOpen(false) }} />

  return (
    <div ref={workspace} className="tutor-workspace tutor-surface tutor-border relative flex overflow-hidden lg:border">
      <aside aria-label={t('dim.bank.myDocuments')} className={cn('tutor-sunken tutor-border hidden shrink-0 flex-col overflow-hidden border-r transition-[width] duration-200 lg:flex',
        !sidebarOpen ? 'w-0 border-r-0' : session && preview.open ? 'w-0 border-r-0 2xl:w-64 2xl:border-r' : 'w-64')}>
        {sidebarOpen && <div className="flex min-h-0 w-64 flex-1 flex-col">{list}</div>}
      </aside>

      <div aria-hidden onClick={() => setDrawerOpen(false)}
        className={cn('absolute inset-0 z-30 bg-slate-900/40 transition-opacity duration-200 lg:hidden', drawerOpen ? 'opacity-100' : 'pointer-events-none opacity-0')} />
      <aside aria-label={t('dim.bank.myDocuments')} inert={!drawerOpen}
        className={cn('tutor-sunken absolute inset-y-0 left-0 z-40 flex w-[82%] max-w-xs flex-col shadow-xl transition-transform duration-250 ease-out lg:hidden', drawerOpen ? 'translate-x-0' : '-translate-x-full')}>
        <div className="flex justify-end px-2 pt-2">
          <button type="button" onClick={() => setDrawerOpen(false)} aria-label={t('tutor.guide.closeList')} className="grid size-9 place-items-center rounded-lg text-slate-500 hover:bg-slate-100"><X className="size-4" /></button>
        </div>
        {list}
      </aside>

      <section className="flex min-w-0 flex-1 flex-col" aria-label={t('nav.items.tutor')}>
        <header className="tutor-border flex h-14 shrink-0 items-center gap-2 border-b px-2 sm:px-3">
          <button type="button" onClick={() => setDrawerOpen(true)} aria-label={t('tutor.guide.openList')} className="grid size-9 place-items-center rounded-lg text-slate-600 hover:bg-slate-100 lg:hidden">
            <Menu className="size-[18px]" />
          </button>
          <button type="button" onClick={toggleSidebar} aria-label={sidebarOpen ? t('tutor.guide.hideList') : t('tutor.guide.showList')} aria-expanded={sidebarOpen}
            className="hidden size-9 place-items-center rounded-lg text-slate-600 hover:bg-slate-100 lg:grid">
            {sidebarOpen ? <PanelLeftClose className="size-[18px]" /> : <PanelLeftOpen className="size-[18px]" />}
          </button>
          {session ? (
            <div className="flex min-w-0 flex-1 items-center gap-2">
              <span className="grid size-8 shrink-0 place-items-center rounded-lg bg-indigo-50 text-indigo-700" aria-hidden><FileText className="size-4" /></span>
              <div className="min-w-0">
                <p className="truncate text-sm font-semibold" title={session.documentName}>{session.documentName}</p>
                <p className="truncate text-xs text-slate-500">{t('tutor.guide.subtitle')}</p>
              </div>
            </div>
          ) : (
            <p className="flex-1 truncate px-1 text-sm font-semibold">{chatId ? '' : courseId ? t('tutor.guide.coursePlan') : t('nav.items.tutor')}</p>
          )}
          <button type="button" onClick={() => openFeedback({ documentName: session?.documentName })} aria-label={t('feedback.title')}
            className="inline-flex h-9 items-center gap-1.5 rounded-lg px-2.5 text-sm font-medium text-slate-600 hover:bg-slate-100">
            <MessageSquareText className="size-[18px]" aria-hidden /><span className="hidden md:inline">{t('feedback.title')}</span>
          </button>
          {session && (
            <button type="button" onClick={() => setPreview(p => ({ ...p, open: !p.open }))} aria-pressed={preview.open}
              className={cn('inline-flex h-9 items-center gap-1.5 rounded-lg px-2.5 text-sm font-medium hover:bg-slate-100', preview.open ? 'text-indigo-700' : 'text-slate-600')}>
              <PanelRight className="size-[18px]" aria-hidden /><span className="hidden sm:inline">{t('questions.document')}</span>
            </button>
          )}
        </header>

        <p role="note" className="tutor-border flex shrink-0 items-center gap-2 border-b bg-amber-50 px-3 py-1.5 text-xs text-amber-800 sm:px-4">
          <span className="shrink-0 rounded-full bg-amber-100 px-2 py-0.5 font-semibold">{t('nav.badge.demo')}</span>
          <span className="min-w-0">
            {t('tutor.guide.demoNote')}{' '}
            <button type="button" onClick={() => openFeedback({ documentName: session?.documentName })} className="font-semibold underline underline-offset-2">{t('tutor.guide.writeUs')}</button>
          </span>
        </p>

        {session && (
          <nav aria-label={t('tutor.guide.sectionsAria')} className="tutor-border shrink-0 border-b px-2 sm:px-3">
            <div role="tablist" className="mx-auto flex max-w-3xl gap-1 overflow-x-auto py-1.5">
              {tabs.map(({ id, label, icon: Icon }) => (
                <button key={id} type="button" role="tab" aria-selected={tab === id} onClick={() => go(id)}
                  className={cn('inline-flex h-9 shrink-0 items-center gap-1.5 rounded-lg px-3 text-sm font-medium whitespace-nowrap transition-colors duration-150',
                    tab === id ? 'bg-slate-100 text-slate-900' : 'text-slate-500 hover:bg-slate-50 hover:text-slate-800')}>
                  <Icon className="size-4" aria-hidden />{t(label)}
                </button>
              ))}
            </div>
          </nav>
        )}

        <div key={`${chatId}-${courseId}-${tab}`} className="min-h-0 flex-1 overflow-y-auto overscroll-contain">
          {!chatId && courseId ? (
            <>
              {createChat.isError && <div className="mx-auto max-w-2xl px-4 pt-5"><Alert title={t('tutor.guide.openFailed')}>{createChat.error.message}</Alert></div>}
              <CoursePanel courseId={courseId} onOpenMaterial={openMaterial} onDeleted={() => setParams({})} />
            </>
          ) : !chatId ? (
            <TutorDocumentPicker onStart={startDocument} starting={createChat.isPending} startError={createChat.error?.message}
              resumable={chats.data?.items.map(c => c.documentId) ?? []} />
          ) : chat.isError ? (
            <div className="p-6"><Alert title={t('documents.notFound')} action={<button type="button" onClick={newDocument} className="text-sm font-semibold underline">{t('tutor.guide.pickDocument')}</button>}>{t('tutor.guide.notFoundText')}</Alert></div>
          ) : session ? (
            <DeckView chatId={session.id} documentId={session.documentId} format={format} tab={tab} scope={scope} onLearn={learn} onSource={showSource} onHit={showHit} />
          ) : (
            <Frame><Skeleton label={t('common.loadingDots')} /></Frame>
          )}
        </div>
      </section>

      {session && preview.open && (
        <aside className="tutor-border absolute inset-0 z-40 flex flex-col xl:static xl:z-auto xl:w-[400px] xl:shrink-0 xl:border-l">
          <TutorPreview documentId={session.documentId} fileName={session.documentName} format={format} source={preview.source}
            onClose={() => setPreview(p => ({ ...p, open: false }))} />
        </aside>
      )}
      {newCourse && <CourseDialog onClose={() => setNewCourse(false)} onSaved={created => { setParams({ course: created.id }); setDrawerOpen(false) }} />}
    </div>
  )
}

function DeckView({ chatId, documentId, format, tab, scope, onLearn, onSource, onHit }: {
  chatId: string; documentId: string; format: string; tab: Tab; scope: LearnScope
  onLearn: (scope: LearnScope) => void; onSource: (card: StudyCard) => void; onHit: (hit: SearchHit) => void
}) {
  const { t } = useTranslation()
  const deck = useDeck(chatId)
  if (deck.isLoading) return <Frame><Skeleton label={t('tutor.guide.preparingCards')} /></Frame>
  if (deck.isError) {
    return (
      <Frame>
        <Alert title={t('tutor.guide.cardsFailed')} action={<button type="button" onClick={() => deck.refetch()} className="text-sm font-semibold underline">{t('common.retry')}</button>}>
          {deck.error.message}
        </Alert>
      </Frame>
    )
  }
  const data = deck.data!
  if (tab === 'learn') return <GuideLearn chatId={chatId} deck={data} scope={scope} onScope={onLearn} onSource={onSource} />
  if (tab === 'cards') return <GuideCardList chatId={chatId} deck={data} onSource={onSource} />
  if (tab === 'search') return <GuideSearch chatId={chatId} deck={data} documentId={documentId} format={format} onOpen={onHit} />
  return <GuideTopics chatId={chatId} documentId={documentId} deck={data} onLearn={onLearn} />
}

interface ListProps {
  items: ChatSession[]; loading: boolean; activeId: string | null
  onSelect: (id: string) => void; onNew: () => void; onDelete: (item: ChatSession) => void
  courses: CourseSummary[]; activeCourseId: string | null; onSelectCourse: (id: string) => void; onNewCourse: () => void
  today: ReviewToday | undefined; onOpenDue: (doc: DueDocument) => void
}

/** "İmtahana 5 gün", "Bu gün imtahan", or nothing without an upcoming exam. */
const courseDays = (course: CourseSummary) =>
  course.daysLeft === null ? null : course.daysLeft === 0 ? i18n.t('courses.examToday') : i18n.t('courses.daysLeft', { count: course.daysLeft })

function DocumentList({ items, loading, activeId, onSelect, onNew, onDelete, courses, activeCourseId, onSelectCourse, onNewCourse, today, onOpenDue }: ListProps) {
  const { t } = useTranslation()
  return (
    <div className="flex min-h-0 w-full flex-1 flex-col">
      <div className="flex flex-col gap-2 p-3">
        <button type="button" onClick={onNew}
          className="tutor-border flex h-10 w-full items-center gap-2 rounded-xl border bg-white px-3 text-sm font-semibold text-slate-800 hover:bg-slate-50">
          <Plus className="size-4" aria-hidden />{t('tutor.guide.pickDocument')}
        </button>
      </div>
      <nav aria-label={t('tutor.guide.listAria')} className="min-h-0 flex-1 overflow-y-auto px-2 pb-3">
        {today && (today.due > 0 || today.reviewedToday > 0) && (
          <section aria-label={t('tutor.guide.todayReview')} className="tutor-border mx-1 mb-3 rounded-xl border bg-white p-3">
            <p className="flex items-center justify-between gap-2 text-xs font-semibold text-slate-700">
              <span>{t('tutor.guide.todayReview')}</span>
              <span className="text-slate-500 tabular-nums" aria-label={t('tutor.guide.todayProgress', { done: today.reviewedToday, goal: today.goal })}>{today.reviewedToday}/{today.goal}</span>
            </p>
            <div className="mt-1.5 h-1.5 overflow-hidden rounded-full bg-slate-100" role="progressbar" aria-valuemin={0} aria-valuemax={today.goal}
              aria-valuenow={Math.min(today.reviewedToday, today.goal)} aria-label={t('tutor.guide.dailyGoal')}>
              <div className="h-full rounded-full bg-emerald-500" style={{ width: `${Math.min(100, (today.reviewedToday / today.goal) * 100)}%` }} />
            </div>
            {today.due === 0 ? (
              <p className="mt-2 text-xs text-emerald-700">{t('tutor.guide.todayDone')}</p>
            ) : (
              <ul className="mt-2 flex flex-col gap-0.5">
                {today.documents.slice(0, 5).map(doc => (
                  <li key={doc.documentId}>
                    <button type="button" onClick={() => onOpenDue(doc)} className="flex w-full items-center gap-2 rounded-md px-1.5 py-1 text-left text-xs text-slate-600 hover:bg-slate-100">
                      <span className="min-w-0 flex-1 truncate">{doc.name}</span>
                      <span className="shrink-0 rounded-full bg-amber-50 px-1.5 font-semibold text-amber-800 tabular-nums">{doc.due}</span>
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </section>
        )}
        <div className="flex items-center justify-between px-3 pt-1 pb-1">
          <p className="text-[11px] font-semibold tracking-wide text-slate-400 uppercase">{t('tutor.guide.myCourses')}</p>
          <button type="button" onClick={onNewCourse} aria-label={t('courses.new')} className="grid size-7 place-items-center rounded-md text-slate-500 hover:bg-slate-200/60 hover:text-slate-800">
            <Plus className="size-3.5" />
          </button>
        </div>
        {courses.length === 0 ? (
          <button type="button" onClick={onNewCourse} className="mx-1 mb-2 flex w-[calc(100%-0.5rem)] items-start gap-2 rounded-lg px-2 py-2 text-left text-xs text-slate-500 hover:bg-slate-200/60">
            <GraduationCap className="mt-0.5 size-4 shrink-0" aria-hidden />{t('tutor.guide.createCourse')}
          </button>
        ) : (
          <ul className="mb-2">
            {courses.map(course => (
              <li key={course.id}>
                <button type="button" onClick={() => onSelectCourse(course.id)} aria-current={course.id === activeCourseId ? 'page' : undefined}
                  className={cn('flex w-full items-center gap-2 rounded-lg px-3 py-2 text-left transition-colors', course.id === activeCourseId ? 'bg-white text-slate-900 shadow-sm' : 'text-slate-600 hover:bg-slate-200/60')}>
                  <GraduationCap className="size-4 shrink-0 text-indigo-600" aria-hidden />
                  <span className="min-w-0 flex-1">
                    <span className="block truncate text-sm font-medium">{course.name}</span>
                    {courseDays(course) && <span className="flex items-center gap-1 text-[11px] text-slate-500"><CalendarDays className="size-3" aria-hidden />{courseDays(course)}</span>}
                  </span>
                </button>
              </li>
            ))}
          </ul>
        )}
        <p className="px-3 pt-2 pb-1 text-[11px] font-semibold tracking-wide text-slate-400 uppercase">{t('dim.bank.myDocuments')}</p>
        {loading ? (
          [0, 1, 2].map(i => <div key={i} className="mx-1 my-1.5 h-9 animate-pulse rounded-lg bg-slate-200/60" />)
        ) : items.length === 0 ? (
          <p className="px-3 py-6 text-center text-xs text-slate-500">{t('tutor.guide.noDocs')}</p>
        ) : (
          <ul>
            {items.map(item => (
              <li key={item.id} className="group relative">
                <button type="button" onClick={() => onSelect(item.id)} aria-current={item.id === activeId ? 'page' : undefined}
                  className={cn('w-full rounded-lg py-2 pr-9 pl-3 text-left transition-colors', item.id === activeId ? 'bg-white text-slate-900 shadow-sm' : 'text-slate-600 hover:bg-slate-200/60')}>
                  <span className="block truncate text-sm font-medium">{item.documentName}</span>
                </button>
                <button type="button" onClick={() => onDelete(item)} aria-label={t('tutor.guide.removeAria', { name: item.documentName })}
                  className="absolute top-1/2 right-1.5 grid size-7 -translate-y-1/2 place-items-center rounded-md text-slate-400 opacity-100 hover:bg-rose-50 hover:text-rose-600 focus-visible:opacity-100 lg:opacity-0 lg:group-hover:opacity-100">
                  <Trash2 className="size-3.5" />
                </button>
              </li>
            ))}
          </ul>
        )}
      </nav>
    </div>
  )
}
