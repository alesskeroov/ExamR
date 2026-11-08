import { UsageMeter } from '../../components/common/UsageMeter'
import { useUsageLimit } from '../usage/api'
import { useQueryClient } from '@tanstack/react-query'
import { ArrowUp, BookOpenText, CircleHelp, FileText, Layers, MessageSquareText, PanelLeftClose, PanelLeftOpen, PanelRight, Plus, Square, Timer, Trash2, X, type LucideIcon } from 'lucide-react'
import { useEffect, useLayoutEffect, useMemo, useRef, useState, type KeyboardEvent as ReactKeyboardEvent } from 'react'
import { useSearchParams } from 'react-router'
import { Alert } from '../../components/common/Alert'
import { ApiError, postEventStream } from '../../lib/api-client'
import { queryKeys } from '../../lib/query-keys'
import { useConfirm } from '../../lib/use-confirm'
import { cn } from '../../lib/utils'
import { useAiUsage, useChat, useChats, useCreateChat, useDeleteChat, useTutorDocuments, type ChatSession, type ChatSource } from './api'
import { TutorDocumentPicker } from './TutorDocumentPicker'
import { TutorMessage, type ViewMessage } from './TutorMessage'
import { TutorPreview } from './TutorPreview'
import { TutorLastMinute } from './TutorLastMinute'
import { TutorQuiz } from './TutorQuiz'
import { TutorShared } from './TutorShared'
import './tutor.css'

const sidebarKey = 'examr-tutor-sidebar'

function readStored(key: string) {
  try { return localStorage.getItem(key) } catch { return null }
}
function store(key: string, value: string) {
  try { localStorage.setItem(key, value) } catch { /* private mode: keep the in-memory choice */ }
}

export function TutorPage() {
  return <TutorWorkspace />
}

interface LiveTurn { user: ViewMessage; answer: ViewMessage; question: string; stored: boolean }

type Tab = 'learn' | 'quiz' | 'shared' | 'last'
const tabs: { id: Tab; label: string; icon: LucideIcon }[] = [
  { id: 'learn', label: 'Öyrən', icon: BookOpenText },
  { id: 'quiz', label: 'Məni yoxla', icon: CircleHelp },
  { id: 'shared', label: 'Ortaq cavab', icon: Layers },
  { id: 'last', label: 'Son dəqiqə', icon: Timer },
]
// Follow-ups for the last answer; retrieval falls back to the previous question, so they stay on the same topic.
const followUps = [
  { label: 'Daha sadə izah et', prompt: 'Son cavabı daha sadə sözlərlə izah et.' },
  { label: 'Geniş izah et', prompt: 'Son cavabı daha geniş izah et.' },
  { label: 'Açar sözlər', prompt: 'Bu mövzunun açar sözlərini qısa siyahı ilə ver.' },
]

function TutorWorkspace() {
  const monthly = useUsageLimit('AiRequest')
  const [params, setParams] = useSearchParams()
  const chatId = params.get('chat')
  const tabParam = params.get('tab')
  const tab: Tab = tabs.some(t => t.id === tabParam) ? tabParam as Tab : 'learn'
  const setTab = (next: Tab) => setParams(next === 'learn' ? { chat: chatId ?? '' } : { chat: chatId ?? '', tab: next }, { replace: true })
  const client = useQueryClient()
  const confirm = useConfirm()
  const chats = useChats()
  const chat = useChat(chatId)
  const documents = useTutorDocuments()
  const usage = useAiUsage()
  const createChat = useCreateChat()
  const deleteChat = useDeleteChat()

  const [sidebarOpen, setSidebarOpen] = useState(() => readStored(sidebarKey) !== 'closed')
  const [drawerOpen, setDrawerOpen] = useState(false)
  const [preview, setPreview] = useState<{ open: boolean; source: ChatSource | null }>({ open: false, source: null })
  const [live, setLive] = useState<LiveTurn | null>(null)
  const abort = useRef<AbortController | null>(null)
  const workspace = useRef<HTMLDivElement>(null)
  const scroller = useRef<HTMLDivElement>(null)
  const stickToBottom = useRef(true)

  const session = chat.data?.session
  const docInfo = documents.data?.find(d => d.documentId === session?.documentId)
  const format = docInfo?.format ?? session?.documentName.split('.').pop()?.toLowerCase() ?? ''
  const streaming = live?.answer.status === 'Streaming'

  // Switching chats drops any in-flight answer of the previous one and closes the mobile drawer.
  const [shownChat, setShownChat] = useState(chatId)
  if (shownChat !== chatId) {
    setShownChat(chatId)
    setLive(null)
    setDrawerOpen(false)
    setPreview({ open: false, source: null })
  }
  useEffect(() => { abort.current?.abort(); stickToBottom.current = true }, [chatId])
  useEffect(() => () => abort.current?.abort(), [])

  // On-screen keyboard: shrink the workspace by the part of the layout viewport the keyboard covers.
  useEffect(() => {
    const viewport = window.visualViewport
    if (!viewport) return
    const update = () => {
      const covered = Math.max(0, window.innerHeight - viewport.height - viewport.offsetTop)
      workspace.current?.style.setProperty('--tutor-keyboard', `${Math.round(covered)}px`)
    }
    viewport.addEventListener('resize', update)
    viewport.addEventListener('scroll', update)
    update()
    return () => { viewport.removeEventListener('resize', update); viewport.removeEventListener('scroll', update) }
  }, [])

  useEffect(() => {
    if (!drawerOpen) return
    const close = (event: KeyboardEvent) => { if (event.key === 'Escape') setDrawerOpen(false) }
    window.addEventListener('keydown', close)
    return () => window.removeEventListener('keydown', close)
  }, [drawerOpen])

  const messages = useMemo<ViewMessage[]>(() => {
    const stored: ViewMessage[] = (chat.data?.messages ?? []).map(m => ({
      key: m.id, role: m.role, content: m.content, status: m.status, grounded: m.grounded, sources: m.sources,
    }))
    // Live messages take their server ids from the meta event, so the saved copies replace them without a remount.
    const ids = new Set(stored.map(m => m.key))
    return live ? [...stored, ...[live.user, live.answer].filter(m => !ids.has(m.key))] : stored
  }, [chat.data, live])

  // Follow the stream only while the reader is already at the bottom.
  useLayoutEffect(() => {
    const el = scroller.current
    if (el && stickToBottom.current) el.scrollTop = el.scrollHeight
  }, [messages])

  const toggleSidebar = () => setSidebarOpen(open => { store(sidebarKey, open ? 'closed' : 'open'); return !open })
  const openSource = (source: ChatSource) => setPreview({ open: true, source })

  const send = async (text: string) => {
    if (!chatId || streaming || monthly.blocked) return
    const controller = new AbortController()
    abort.current = controller
    stickToBottom.current = true
    const turn: LiveTurn = {
      question: text, stored: false,
      user: { key: 'live-user', role: 'User', content: text, status: 'Completed', grounded: null, sources: [] },
      answer: { key: 'live-answer', role: 'Assistant', content: '', status: 'Streaming', grounded: null, sources: [] },
    }
    setLive(turn)
    const patch = (change: (answer: ViewMessage) => ViewMessage) =>
      setLive(current => current && { ...current, answer: change(current.answer) })

    let stored = false
    try {
      await postEventStream(`/learning/chats/${chatId}/messages`, { content: text }, controller.signal, (event, data) => {
        const payload = data as Record<string, unknown>
        if (event === 'meta') {
          stored = true
          setLive(current => current && {
            ...current, stored: true,
            user: { ...current.user, key: payload.userMessageId as string },
            answer: { ...current.answer, key: payload.assistantMessageId as string, grounded: payload.grounded as boolean, sources: payload.sources as ChatSource[] },
          })
        }
        else if (event === 'delta') patch(a => ({ ...a, content: a.content + (payload.text as string) }))
        else if (event === 'error') patch(a => ({ ...a, error: payload.detail as string }))
        else if (event === 'done') patch(a => ({ ...a, status: payload.status as ViewMessage['status'] }))
      })
      patch(a => (a.status === 'Streaming' ? { ...a, status: 'Completed' } : a))
    } catch (error) {
      if (controller.signal.aborted) patch(a => ({ ...a, status: 'Cancelled' }))
      else patch(a => ({ ...a, status: 'Failed', error: error instanceof ApiError || error instanceof Error ? error.message : 'Şəbəkə xətası.' }))
    } finally {
      void client.invalidateQueries({ queryKey: queryKeys.usage.mine })
      if (abort.current === controller) abort.current = null
    }
    void client.invalidateQueries({ queryKey: queryKeys.tutor.usage })
    void client.invalidateQueries({ queryKey: queryKeys.tutor.chats })
    if (stored) {
      // The server saves a stopped answer once it notices the disconnect; give it a moment before reloading.
      if (controller.signal.aborted) await new Promise(resolve => setTimeout(resolve, 700))
      await client.refetchQueries({ queryKey: queryKeys.tutor.chat(chatId) })
      setLive(null)
    }
    // Not stored (rejected before streaming: limit, budget, network): keep it on screen with a retry button.
  }

  const stop = () => abort.current?.abort()

  const startChat = (documentId: string) =>
    createChat.mutate(documentId, { onSuccess: created => setParams({ chat: created.id }) })

  const removeChat = async (target: ChatSession) => {
    const ok = await confirm({ title: 'Söhbət silinsin?', description: `"${target.title}" və bütün mesajları silinəcək. Bu əməliyyat geri qaytarılmır.`, confirmLabel: 'Sil', danger: true })
    if (!ok) return
    deleteChat.mutate(target.id, { onSuccess: () => { if (target.id === chatId) setParams({}) } })
  }

  const newChat = () => { setParams({}); setDrawerOpen(false) }
  const retryFor = (index: number) => {
    if (live && index >= messages.length - 2) {
      const { question } = live
      return () => { if (!live.stored) setLive(null); void send(question) }
    }
    const question = [...messages.slice(0, index)].reverse().find(m => m.role === 'User')?.content
    return question ? () => void send(question) : undefined
  }

  const history = (
    <ChatHistory chats={chats.data?.items ?? []} loading={chats.isLoading} activeId={chatId}
      onSelect={id => setParams({ chat: id })} onNew={newChat} onDelete={removeChat}
      usage={usage.data?.remainingUsd != null ? `${usage.data.remainingUsd.toFixed(2)} $ qalıb · bu gün ${usage.data.requestsToday}/${usage.data.dailyRequestsPerUser}` : null} />
  )

  return (
    <div ref={workspace} className="tutor-workspace tutor-surface tutor-border relative flex overflow-hidden lg:border">
      {/* Desktop history column */}
      {/* With the preview open the chat would get too narrow, so history steps aside below 2xl. */}
      <aside aria-label="Söhbət tarixçəsi" className={cn('tutor-sunken tutor-border hidden shrink-0 flex-col overflow-hidden border-r transition-[width] duration-200 lg:flex',
        !sidebarOpen ? 'w-0 border-r-0' : session && preview.open ? 'w-0 border-r-0 2xl:w-64 2xl:border-r' : 'w-64')}>
        {sidebarOpen && <div className="flex min-h-0 w-64 flex-1 flex-col">{history}</div>}
      </aside>

      {/* Mobile history drawer */}
      <div aria-hidden onClick={() => setDrawerOpen(false)}
        className={cn('absolute inset-0 z-30 bg-slate-900/40 transition-opacity duration-200 lg:hidden', drawerOpen ? 'opacity-100' : 'pointer-events-none opacity-0')} />
      <aside aria-label="Söhbət tarixçəsi" inert={!drawerOpen}
        className={cn('tutor-sunken absolute inset-y-0 left-0 z-40 flex w-[82%] max-w-xs flex-col shadow-xl transition-transform duration-250 ease-out lg:hidden', drawerOpen ? 'translate-x-0' : '-translate-x-full')}>
        <div className="flex justify-end px-2 pt-2">
          <button type="button" onClick={() => setDrawerOpen(false)} aria-label="Tarixçəni bağla" className="grid size-9 place-items-center rounded-lg text-slate-500 hover:bg-slate-100"><X className="size-4" /></button>
        </div>
        {history}
      </aside>

      {/* Conversation */}
      <section className="flex min-w-0 flex-1 flex-col" aria-label="Söhbət">
        <header className="tutor-border flex h-14 shrink-0 items-center gap-2 border-b px-2 sm:px-3">
          <button type="button" onClick={() => setDrawerOpen(true)} aria-label="Tarixçəni aç" className="grid size-9 place-items-center rounded-lg text-slate-600 hover:bg-slate-100 lg:hidden">
            <MessageSquareText className="size-[18px]" />
          </button>
          <button type="button" onClick={toggleSidebar} aria-label={sidebarOpen ? 'Tarixçəni gizlət' : 'Tarixçəni göstər'} aria-expanded={sidebarOpen}
            className="hidden size-9 place-items-center rounded-lg text-slate-600 hover:bg-slate-100 lg:grid">
            {sidebarOpen ? <PanelLeftClose className="size-[18px]" /> : <PanelLeftOpen className="size-[18px]" />}
          </button>
          {session ? (
            <div className="flex min-w-0 flex-1 items-center gap-2">
              <span className="grid size-8 shrink-0 place-items-center rounded-lg bg-indigo-50 text-indigo-700" aria-hidden><FileText className="size-4" /></span>
              <div className="min-w-0">
                <p className="truncate text-sm font-semibold" title={session.documentName}>{session.documentName}</p>
                <p className="truncate text-xs text-slate-500">{docInfo?.pageCount ? `${docInfo.pageCount} səhifə · ` : ''}Cavablar yalnız bu sənədə əsaslanır</p>
              </div>
            </div>
          ) : (
            <p className="flex-1 truncate px-1 text-sm font-semibold">{chatId ? '' : 'Yeni söhbət'}</p>
          )}
          <span className="shrink-0 rounded-full bg-amber-100 px-2 py-0.5 text-[11px] font-semibold text-amber-800" title="AI Tutor sınaq (demo) mərhələsindədir">Demo</span>
          {session && (
            <button type="button" onClick={() => setPreview(p => ({ ...p, open: !p.open }))} aria-pressed={preview.open}
              className={cn('inline-flex h-9 items-center gap-1.5 rounded-lg px-2.5 text-sm font-medium hover:bg-slate-100', preview.open ? 'text-indigo-700' : 'text-slate-600')}>
              <PanelRight className="size-[18px]" aria-hidden /><span className="hidden sm:inline">Sənəd</span>
            </button>
          )}
        </header>

        {usage.data && !usage.data.enabled && (
          <div className="px-3 pt-3"><Alert tone="info" title="AI köməkçisi hazırda söndürülüb">Tarixçəyə baxmaq olar, yeni suallar isə qəbul edilmir.</Alert></div>
        )}

        {session && (
          <nav aria-label="AI Tutor bölmələri" className="tutor-border shrink-0 border-b px-2 sm:px-3">
            <div role="tablist" className="tutor-tabs mx-auto flex max-w-3xl gap-1 overflow-x-auto py-1.5">
              {tabs.map(({ id, label, icon: Icon }) => (
                <button key={id} type="button" role="tab" aria-selected={tab === id}
                  onClick={event => { setTab(id); event.currentTarget.scrollIntoView({ block: 'nearest', inline: 'nearest' }) }}
                  className={cn('inline-flex h-9 shrink-0 items-center gap-1.5 rounded-lg px-2.5 text-[13px] font-medium whitespace-nowrap transition-colors duration-150 sm:px-3 sm:text-sm',
                    tab === id ? 'bg-slate-100 text-slate-900' : 'text-slate-500 hover:bg-slate-50 hover:text-slate-800')}>
                  <Icon className="hidden size-4 sm:block" aria-hidden />{label}
                </button>
              ))}
            </div>
          </nav>
        )}

        {!chatId ? (
          <div className="min-h-0 flex-1 overflow-y-auto"><TutorDocumentPicker onStart={startChat} starting={createChat.isPending} /></div>
        ) : chat.isError ? (
          <div className="p-6"><Alert title="Söhbət tapılmadı" action={<button type="button" onClick={newChat} className="text-sm font-semibold underline">Yeni söhbət</button>}>Silinmiş ola bilər və ya sizə aid deyil.</Alert></div>
        ) : session && tab !== 'learn' ? (
          <div className="min-h-0 flex-1 overflow-y-auto overscroll-contain">
            {usage.data?.enabled && <UsageMeter kind="AiRequest" />}
            {tab === 'quiz' && <TutorQuiz key={chatId} chatId={chatId} disabled={monthly.blocked || usage.data?.enabled === false} />}
            {tab === 'shared' && <TutorShared key={chatId} chatId={chatId} disabled={monthly.blocked || usage.data?.enabled === false} />}
            {tab === 'last' && <TutorLastMinute key={chatId} chatId={chatId} disabled={monthly.blocked || usage.data?.enabled === false} />}
          </div>
        ) : (
          <>
            <div ref={scroller} className="min-h-0 flex-1 overflow-y-auto overscroll-contain"
              onScroll={e => { const el = e.currentTarget; stickToBottom.current = el.scrollHeight - el.scrollTop - el.clientHeight < 80 }}>
              <div className="mx-auto flex w-full max-w-3xl flex-col gap-6 px-4 py-6" aria-live="polite" aria-relevant="additions">
                {chat.isLoading ? (
                  [0, 1].map(i => <div key={i} className="h-16 animate-pulse rounded-xl bg-slate-100" />)
                ) : messages.length === 0 ? (
                  <Welcome onAsk={text => send(text)} />
                ) : (
                  messages.map((message, index) => (
                    <TutorMessage key={message.key} message={message} onOpenSource={openSource}
                      onRetry={message.role === 'Assistant' && message.status !== 'Streaming' && (message.status !== 'Completed' || index === messages.length - 1) && !streaming ? retryFor(index) : undefined} />
                  ))
                )}
              </div>
            </div>
            {session && !streaming && messages.at(-1)?.role === 'Assistant' && messages.at(-1)?.status === 'Completed' && messages.at(-1)?.grounded !== false && (
              <div className="shrink-0 px-3 sm:px-4">
                <div className="mx-auto flex max-w-3xl gap-1.5 overflow-x-auto pb-1 [scrollbar-width:none]" aria-label="Son cavab üçün">
                  {followUps.map(f => (
                    <button key={f.label} type="button" onClick={() => void send(f.prompt)} disabled={monthly.blocked || usage.data?.enabled === false}
                      className="tutor-border inline-flex h-8 shrink-0 items-center rounded-full border bg-white px-3 text-xs font-medium whitespace-nowrap text-slate-700 hover:border-indigo-200 hover:bg-indigo-50/50 disabled:opacity-50">
                      {f.label}
                    </button>
                  ))}
                </div>
              </div>
            )}
            {usage.data?.enabled && <UsageMeter kind="AiRequest" />}
            <Composer key={chatId} disabled={monthly.blocked || !session || usage.data?.enabled === false} streaming={streaming} onSend={text => send(text)} onStop={stop} />
          </>
        )}
      </section>

      {/* Document preview: side column on wide screens, full sheet on small ones */}
      {session && preview.open && (
        <aside className="tutor-border absolute inset-0 z-40 flex flex-col xl:static xl:z-auto xl:w-[400px] xl:shrink-0 xl:border-l">
          <TutorPreview documentId={session.documentId} fileName={session.documentName} format={format} source={preview.source}
            onClose={() => setPreview(p => ({ ...p, open: false }))} />
        </aside>
      )}
    </div>
  )
}

function Welcome({ onAsk }: { onAsk: (text: string) => void }) {
  const prompts = ['Sənədin əsas mövzuları hansılardır?', 'Əsas anlayışları qısa siyahı şəklində ver', 'Ən vacib tərifləri izah et']
  return (
    <div className="tutor-enter py-8 text-center sm:py-14">
      <span className="mx-auto grid size-12 place-items-center rounded-2xl bg-indigo-50 text-indigo-700" aria-hidden>
        <BookOpenText className="size-6" />
      </span>
      <h2 className="mt-4 text-xl font-semibold tracking-tight">Sənəd haqqında soruşun</h2>
      <p className="mx-auto mt-1.5 max-w-md text-sm text-slate-600">Hər cavab sənəddəki mənbələrə istinad edir. Sənəddə olmayan məlumat uydurulmur.</p>
      <div className="mx-auto mt-6 grid max-w-xl gap-2 sm:grid-cols-3">
        {prompts.map(prompt => (
          <button key={prompt} type="button" onClick={() => onAsk(prompt)}
            className="tutor-border rounded-xl border px-3 py-3 text-left text-sm text-slate-700 transition-colors hover:border-indigo-200 hover:bg-indigo-50/50">
            {prompt}
          </button>
        ))}
      </div>
    </div>
  )
}

interface ComposerProps {
  disabled: boolean; streaming: boolean
  onSend: (text: string) => void; onStop: () => void
}

function Composer({ disabled, streaming, onSend, onStop }: ComposerProps) {
  const [text, setText] = useState('')
  const area = useRef<HTMLTextAreaElement>(null)
  useEffect(() => { area.current?.focus({ preventScroll: true }) }, [])
  useLayoutEffect(() => {
    const el = area.current
    if (!el) return
    el.style.height = 'auto'
    el.style.height = `${Math.min(el.scrollHeight, 200)}px`
  }, [text])

  const submit = () => {
    const value = text.trim()
    if (!value || disabled || streaming) return
    onSend(value)
    setText('')
  }
  const onKeyDown = (event: ReactKeyboardEvent<HTMLTextAreaElement>) => {
    if (event.key === 'Enter' && !event.shiftKey && !event.nativeEvent.isComposing) { event.preventDefault(); submit() }
    else if (event.key === 'Escape' && streaming) onStop()
  }

  return (
    <div className="tutor-composer-safe shrink-0 px-3 pt-2 sm:px-4">
      <form className="tutor-border mx-auto max-w-3xl rounded-2xl border bg-white shadow-[0_8px_30px_-18px_rgb(21_23_42/35%)] focus-within:border-indigo-300"
        onSubmit={e => { e.preventDefault(); submit() }}>
        <label htmlFor="tutor-input" className="sr-only">Sual</label>
        <textarea id="tutor-input" ref={area} rows={1} value={text} onChange={e => setText(e.target.value)} onKeyDown={onKeyDown}
          maxLength={1500} disabled={disabled} enterKeyHint="send"
          placeholder={disabled ? 'Söhbət hazırda əlçatan deyil' : 'Sənəd haqqında sual verin…'}
          className="block max-h-[200px] w-full resize-none bg-transparent px-4 pt-3 pb-1 text-[15px] leading-relaxed outline-none placeholder:text-slate-400 disabled:cursor-not-allowed" />
        <div className="flex items-center gap-2 px-2 pb-2">
          <span className="ml-auto hidden min-w-0 truncate text-[11px] text-slate-400 md:inline">Enter — göndər · Shift+Enter — yeni sətir</span>
          {streaming ? (
            <button type="button" onClick={onStop} aria-label="Cavabı dayandır" title="Dayandır (Esc)"
              className="ml-auto grid size-9 shrink-0 place-items-center rounded-full bg-slate-900 text-white hover:bg-slate-700 md:ml-0">
              <Square className="size-3.5 fill-current" />
            </button>
          ) : (
            <button type="submit" aria-label="Göndər" disabled={disabled || !text.trim()}
              className="btn-primary ml-auto grid size-9 shrink-0 place-items-center rounded-full disabled:opacity-40 md:ml-0">
              <ArrowUp className="size-4" />
            </button>
          )}
        </div>
      </form>
      <p className="mx-auto mt-1.5 max-w-3xl text-center text-[11px] text-slate-400">AI səhv edə bilər — vacib məlumatı mənbədə yoxlayın.</p>
    </div>
  )
}

interface HistoryProps {
  chats: ChatSession[]; loading: boolean; activeId: string | null; usage: string | null
  onSelect: (id: string) => void; onNew: () => void; onDelete: (chat: ChatSession) => void
}

function ChatHistory({ chats, loading, activeId, usage, onSelect, onNew, onDelete }: HistoryProps) {
  const [startOfToday] = useState(() => { const d = new Date(); d.setHours(0, 0, 0, 0); return d.getTime() })
  const groups = useMemo(() => {
    const today = new Date(startOfToday)
    const yesterday = new Date(today); yesterday.setDate(today.getDate() - 1)
    const week = new Date(today); week.setDate(today.getDate() - 7)
    const buckets: [string, ChatSession[]][] = [['Bu gün', []], ['Dünən', []], ['Son 7 gün', []], ['Daha əvvəl', []]]
    for (const chat of chats) {
      const at = new Date(chat.updatedAt)
      buckets[at >= today ? 0 : at >= yesterday ? 1 : at >= week ? 2 : 3][1].push(chat)
    }
    return buckets.filter(([, items]) => items.length > 0)
  }, [chats, startOfToday])

  return (
    <div className="flex min-h-0 w-full flex-1 flex-col">
      <div className="p-3">
        <button type="button" onClick={onNew}
          className="tutor-border flex h-10 w-full items-center gap-2 rounded-xl border bg-white px-3 text-sm font-semibold text-slate-800 hover:bg-slate-50">
          <Plus className="size-4" aria-hidden />Yeni söhbət
        </button>
      </div>
      <nav aria-label="Köhnə söhbətlər" className="min-h-0 flex-1 overflow-y-auto px-2 pb-3">
        {loading ? (
          [0, 1, 2, 3].map(i => <div key={i} className="mx-1 my-1.5 h-9 animate-pulse rounded-lg bg-slate-200/60" />)
        ) : chats.length === 0 ? (
          <p className="px-3 py-6 text-center text-xs text-slate-500">Hələ söhbət yoxdur.</p>
        ) : groups.map(([label, items]) => (
          <div key={label} className="mt-2 first:mt-0">
            <p className="px-3 pt-2 pb-1 text-[11px] font-semibold tracking-wide text-slate-400 uppercase">{label}</p>
            <ul>
              {items.map(item => (
                <li key={item.id} className="group relative">
                  <button type="button" onClick={() => onSelect(item.id)} aria-current={item.id === activeId ? 'page' : undefined}
                    className={cn('w-full rounded-lg py-2 pr-9 pl-3 text-left transition-colors', item.id === activeId ? 'bg-white text-slate-900 shadow-sm' : 'text-slate-600 hover:bg-slate-200/60')}>
                    <span className="block truncate text-sm font-medium">{item.title}</span>
                    <span className="block truncate text-[11px] text-slate-400">{item.documentName}</span>
                  </button>
                  <button type="button" onClick={() => onDelete(item)} aria-label={`"${item.title}" söhbətini sil`}
                    className="absolute top-1/2 right-1.5 grid size-7 -translate-y-1/2 place-items-center rounded-md text-slate-400 opacity-100 hover:bg-rose-50 hover:text-rose-600 focus-visible:opacity-100 lg:opacity-0 lg:group-hover:opacity-100">
                    <Trash2 className="size-3.5" />
                  </button>
                </li>
              ))}
            </ul>
          </div>
        ))}
      </nav>
      {usage && <p className="tutor-border border-t px-4 py-2.5 text-[11px] text-slate-500">AI büdcəsi: {usage}</p>}
    </div>
  )
}
