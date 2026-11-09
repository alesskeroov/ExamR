import { FileText, Layers, LibraryBig, ListChecks, Menu, MessageSquareText, PanelLeftClose, PanelLeftOpen, PanelRight, Plus, Trash2, X, type LucideIcon } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { useSearchParams } from 'react-router'
import { Alert } from '../../components/common/Alert'
import { useConfirm } from '../../lib/use-confirm'
import { cn } from '../../lib/utils'
import { useChat, useChats, useCreateChat, useDeck, useDeleteChat, useTutorDocuments, type ChatSession, type ChatSource, type LearnScope, type StudyCard } from './api'
import { FeedbackDialog } from './FeedbackDialog'
import { GuideCardList } from './GuideCardList'
import { GuideLearn } from './GuideLearn'
import { GuideTopics } from './GuideTopics'
import { TutorDocumentPicker } from './TutorDocumentPicker'
import { TutorPreview } from './TutorPreview'
import { Frame, Skeleton } from './TutorShared'
import './tutor.css'

const sidebarKey = 'examr-guide-sidebar'

function readStored(key: string) {
  try { return localStorage.getItem(key) } catch { return null }
}
function store(key: string, value: string) {
  try { localStorage.setItem(key, value) } catch { /* private mode: keep the in-memory choice */ }
}

type Tab = 'topics' | 'learn' | 'cards'
const tabs: { id: Tab; label: string; icon: LucideIcon }[] = [
  { id: 'topics', label: 'Mövzular', icon: LibraryBig },
  { id: 'learn', label: 'Öyrən', icon: Layers },
  { id: 'cards', label: 'Kartlarım', icon: ListChecks },
]
const tabFrom = (value: string | null): Tab => value === 'learn' || value === 'cards' ? value : 'topics'

function scopeFrom(params: URLSearchParams): LearnScope {
  const scope = params.get('scope')
  const topic = params.get('topic')
  if (scope === 'topic' && topic) return { kind: 'topic', key: topic }
  return scope === 'weak' || scope === 'mixed' ? { kind: scope } : { kind: 'all' }
}

/**
 * Öyrənmə bələdçisi: the document's own questions as private flashcards, without any AI call.
 * (The former AI Tutor page, TutorPage, is kept in the code but no longer routed.)
 */
export function GuidePage() {
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
  const [feedback, setFeedback] = useState(false)
  const workspace = useRef<HTMLDivElement>(null)

  const session = chat.data?.session
  const docInfo = documents.data?.find(d => d.documentId === session?.documentId)
  const format = docInfo?.format ?? session?.documentName.split('.').pop()?.toLowerCase() ?? ''

  const [shownChat, setShownChat] = useState(chatId)
  if (shownChat !== chatId) {
    setShownChat(chatId)
    setDrawerOpen(false)
    setPreview({ open: false, source: null })
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

  const toggleSidebar = () => setSidebarOpen(open => { store(sidebarKey, open ? 'closed' : 'open'); return !open })
  // One entry per document: an existing one is reopened with its progress.
  const startDocument = (documentId: string) => {
    const existing = chats.data?.items.find(c => c.documentId === documentId)
    if (existing) setParams({ chat: existing.id })
    else createChat.mutate(documentId, { onSuccess: created => setParams({ chat: created.id }) })
  }
  const remove = async (target: ChatSession) => {
    const ok = await confirm({
      title: 'Sənəd siyahıdan çıxarılsın?', confirmLabel: 'Çıxar', danger: true,
      description: `"${target.documentName}" siyahıdan çıxarılacaq. Kart irəliləyişin saxlanılır və sənədi yenidən açanda qayıdır.`,
    })
    if (!ok) return
    deleteChat.mutate(target.id, { onSuccess: () => { if (target.id === chatId) setParams({}) } })
  }
  const newDocument = () => { setParams({}); setDrawerOpen(false) }

  const list = <DocumentList items={chats.data?.items ?? []} loading={chats.isLoading} activeId={chatId}
    onSelect={id => setParams({ chat: id })} onNew={newDocument} onDelete={remove} />

  return (
    <div ref={workspace} className="tutor-workspace tutor-surface tutor-border relative flex overflow-hidden lg:border">
      <aside aria-label="Sənədlərim" className={cn('tutor-sunken tutor-border hidden shrink-0 flex-col overflow-hidden border-r transition-[width] duration-200 lg:flex',
        !sidebarOpen ? 'w-0 border-r-0' : session && preview.open ? 'w-0 border-r-0 2xl:w-64 2xl:border-r' : 'w-64')}>
        {sidebarOpen && <div className="flex min-h-0 w-64 flex-1 flex-col">{list}</div>}
      </aside>

      <div aria-hidden onClick={() => setDrawerOpen(false)}
        className={cn('absolute inset-0 z-30 bg-slate-900/40 transition-opacity duration-200 lg:hidden', drawerOpen ? 'opacity-100' : 'pointer-events-none opacity-0')} />
      <aside aria-label="Sənədlərim" inert={!drawerOpen}
        className={cn('tutor-sunken absolute inset-y-0 left-0 z-40 flex w-[82%] max-w-xs flex-col shadow-xl transition-transform duration-250 ease-out lg:hidden', drawerOpen ? 'translate-x-0' : '-translate-x-full')}>
        <div className="flex justify-end px-2 pt-2">
          <button type="button" onClick={() => setDrawerOpen(false)} aria-label="Siyahını bağla" className="grid size-9 place-items-center rounded-lg text-slate-500 hover:bg-slate-100"><X className="size-4" /></button>
        </div>
        {list}
      </aside>

      <section className="flex min-w-0 flex-1 flex-col" aria-label="Öyrənmə bələdçisi">
        <header className="tutor-border flex h-14 shrink-0 items-center gap-2 border-b px-2 sm:px-3">
          <button type="button" onClick={() => setDrawerOpen(true)} aria-label="Sənədlərimi aç" className="grid size-9 place-items-center rounded-lg text-slate-600 hover:bg-slate-100 lg:hidden">
            <Menu className="size-[18px]" />
          </button>
          <button type="button" onClick={toggleSidebar} aria-label={sidebarOpen ? 'Siyahını gizlət' : 'Siyahını göstər'} aria-expanded={sidebarOpen}
            className="hidden size-9 place-items-center rounded-lg text-slate-600 hover:bg-slate-100 lg:grid">
            {sidebarOpen ? <PanelLeftClose className="size-[18px]" /> : <PanelLeftOpen className="size-[18px]" />}
          </button>
          {session ? (
            <div className="flex min-w-0 flex-1 items-center gap-2">
              <span className="grid size-8 shrink-0 place-items-center rounded-lg bg-indigo-50 text-indigo-700" aria-hidden><FileText className="size-4" /></span>
              <div className="min-w-0">
                <p className="truncate text-sm font-semibold" title={session.documentName}>{session.documentName}</p>
                <p className="truncate text-xs text-slate-500">Sənəddən çıxarılan kartlar və konspekt</p>
              </div>
            </div>
          ) : (
            <p className="flex-1 truncate px-1 text-sm font-semibold">{chatId ? '' : 'Öyrənmə bələdçisi'}</p>
          )}
          <button type="button" onClick={() => setFeedback(true)} aria-label="Təklif və iradlar"
            className="inline-flex h-9 items-center gap-1.5 rounded-lg px-2.5 text-sm font-medium text-slate-600 hover:bg-slate-100">
            <MessageSquareText className="size-[18px]" aria-hidden /><span className="hidden md:inline">Təklif və iradlar</span>
          </button>
          {session && (
            <button type="button" onClick={() => setPreview(p => ({ ...p, open: !p.open }))} aria-pressed={preview.open}
              className={cn('inline-flex h-9 items-center gap-1.5 rounded-lg px-2.5 text-sm font-medium hover:bg-slate-100', preview.open ? 'text-indigo-700' : 'text-slate-600')}>
              <PanelRight className="size-[18px]" aria-hidden /><span className="hidden sm:inline">Sənəd</span>
            </button>
          )}
        </header>

        <p role="note" className="tutor-border flex shrink-0 items-center gap-2 border-b bg-amber-50 px-3 py-1.5 text-xs text-amber-800 sm:px-4">
          <span className="shrink-0 rounded-full bg-amber-100 px-2 py-0.5 font-semibold">Demo</span>
          <span className="min-w-0">
            Bələdçi sınaq mərhələsindədir, kartlarda səhv ola bilər.{' '}
            <button type="button" onClick={() => setFeedback(true)} className="font-semibold underline underline-offset-2">Bizə yazın</button>
          </span>
        </p>

        {session && (
          <nav aria-label="Bələdçi bölmələri" className="tutor-border shrink-0 border-b px-2 sm:px-3">
            <div role="tablist" className="mx-auto flex max-w-3xl gap-1 overflow-x-auto py-1.5">
              {tabs.map(({ id, label, icon: Icon }) => (
                <button key={id} type="button" role="tab" aria-selected={tab === id} onClick={() => go(id)}
                  className={cn('inline-flex h-9 shrink-0 items-center gap-1.5 rounded-lg px-3 text-sm font-medium whitespace-nowrap transition-colors duration-150',
                    tab === id ? 'bg-slate-100 text-slate-900' : 'text-slate-500 hover:bg-slate-50 hover:text-slate-800')}>
                  <Icon className="size-4" aria-hidden />{label}
                </button>
              ))}
            </div>
          </nav>
        )}

        <div key={`${chatId}-${tab}`} className="min-h-0 flex-1 overflow-y-auto overscroll-contain">
          {!chatId ? (
            <TutorDocumentPicker onStart={startDocument} starting={createChat.isPending} startError={createChat.error?.message}
              resumable={chats.data?.items.map(c => c.documentId) ?? []} />
          ) : chat.isError ? (
            <div className="p-6"><Alert title="Sənəd tapılmadı" action={<button type="button" onClick={newDocument} className="text-sm font-semibold underline">Sənəd seç</button>}>Silinmiş ola bilər və ya sizə aid deyil.</Alert></div>
          ) : session ? (
            <DeckView chatId={session.id} tab={tab} scope={scope} onLearn={learn} onSource={showSource} />
          ) : (
            <Frame><Skeleton label="Yüklənir…" /></Frame>
          )}
        </div>
      </section>

      {session && preview.open && (
        <aside className="tutor-border absolute inset-0 z-40 flex flex-col xl:static xl:z-auto xl:w-[400px] xl:shrink-0 xl:border-l">
          <TutorPreview documentId={session.documentId} fileName={session.documentName} format={format} source={preview.source}
            onClose={() => setPreview(p => ({ ...p, open: false }))} />
        </aside>
      )}
      {feedback && <FeedbackDialog documentName={session?.documentName} onClose={() => setFeedback(false)} />}
    </div>
  )
}

function DeckView({ chatId, tab, scope, onLearn, onSource }: {
  chatId: string; tab: Tab; scope: LearnScope; onLearn: (scope: LearnScope) => void; onSource: (card: StudyCard) => void
}) {
  const deck = useDeck(chatId)
  if (deck.isLoading) return <Frame><Skeleton label="Kartlar hazırlanır…" /></Frame>
  if (deck.isError) {
    return (
      <Frame>
        <Alert title="Kartlar hazırlanmadı" action={<button type="button" onClick={() => deck.refetch()} className="text-sm font-semibold underline">Yenidən cəhd et</button>}>
          {deck.error.message}
        </Alert>
      </Frame>
    )
  }
  const data = deck.data!
  if (tab === 'learn') return <GuideLearn chatId={chatId} deck={data} scope={scope} onScope={onLearn} onSource={onSource} />
  if (tab === 'cards') return <GuideCardList chatId={chatId} deck={data} onSource={onSource} />
  return <GuideTopics deck={data} onLearn={onLearn} />
}

interface ListProps {
  items: ChatSession[]; loading: boolean; activeId: string | null
  onSelect: (id: string) => void; onNew: () => void; onDelete: (item: ChatSession) => void
}

function DocumentList({ items, loading, activeId, onSelect, onNew, onDelete }: ListProps) {
  return (
    <div className="flex min-h-0 w-full flex-1 flex-col">
      <div className="p-3">
        <button type="button" onClick={onNew}
          className="tutor-border flex h-10 w-full items-center gap-2 rounded-xl border bg-white px-3 text-sm font-semibold text-slate-800 hover:bg-slate-50">
          <Plus className="size-4" aria-hidden />Sənəd seç
        </button>
      </div>
      <nav aria-label="Sənədlərim" className="min-h-0 flex-1 overflow-y-auto px-2 pb-3">
        <p className="px-3 pt-2 pb-1 text-[11px] font-semibold tracking-wide text-slate-400 uppercase">Sənədlərim</p>
        {loading ? (
          [0, 1, 2].map(i => <div key={i} className="mx-1 my-1.5 h-9 animate-pulse rounded-lg bg-slate-200/60" />)
        ) : items.length === 0 ? (
          <p className="px-3 py-6 text-center text-xs text-slate-500">Hələ sənəd yoxdur.</p>
        ) : (
          <ul>
            {items.map(item => (
              <li key={item.id} className="group relative">
                <button type="button" onClick={() => onSelect(item.id)} aria-current={item.id === activeId ? 'page' : undefined}
                  className={cn('w-full rounded-lg py-2 pr-9 pl-3 text-left transition-colors', item.id === activeId ? 'bg-white text-slate-900 shadow-sm' : 'text-slate-600 hover:bg-slate-200/60')}>
                  <span className="block truncate text-sm font-medium">{item.documentName}</span>
                </button>
                <button type="button" onClick={() => onDelete(item)} aria-label={`"${item.documentName}" siyahıdan çıxar`}
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
