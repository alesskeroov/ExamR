import { Bell, Check, Trash2, X } from 'lucide-react'
import { useEffect, useId, useRef, useState } from 'react'
import { formatDate } from '../../lib/format'
import { cn } from '../../lib/utils'
import { useNotificationAction, useNotifications, type SocialNotification } from '../friends/api'
import { useDeleteNotifications } from '../friends/notification-management'

/**
 * Bell for the exam screen: opens a small panel in place instead of navigating to /notifications, so the running
 * exam (answers, current question, timer) is never left. Actions that would leave the exam are not offered here.
 */
export function ExamNotifications() {
  const notifications = useNotifications()
  const read = useNotificationAction()
  const remove = useDeleteNotifications()
  const [open, setOpen] = useState(false)
  const root = useRef<HTMLDivElement>(null)
  const button = useRef<HTMLButtonElement>(null)
  const panelId = useId()
  const count = notifications.data?.unreadCount ?? 0
  const items = (notifications.data?.items ?? []).slice(0, 8)

  useEffect(() => {
    if (!open) return
    const onPointer = (event: PointerEvent) => { if (!root.current?.contains(event.target as Node)) setOpen(false) }
    // Escape closes only the panel; the exam's own key handling ignores keys while the panel is focused.
    const onKey = (event: KeyboardEvent) => {
      if (event.key !== 'Escape') return
      event.stopPropagation()
      setOpen(false)
      button.current?.focus()
    }
    document.addEventListener('pointerdown', onPointer)
    document.addEventListener('keydown', onKey, true)
    return () => { document.removeEventListener('pointerdown', onPointer); document.removeEventListener('keydown', onKey, true) }
  }, [open])

  return (
    <div ref={root} className="exam-notify shrink-0">
      <button ref={button} type="button" onClick={() => setOpen(value => !value)} aria-expanded={open} aria-controls={panelId} aria-haspopup="dialog"
        aria-label={`Bildirişlər${count ? `, ${count} oxunmamış` : ''}`}
        className="relative grid size-10 place-items-center rounded-lg text-slate-600 transition-colors duration-150 hover:bg-slate-100 hover:text-slate-900">
        <Bell className="size-5" aria-hidden />
        {count > 0 && <span aria-hidden className="absolute -top-0.5 -right-0.5 flex min-w-5 items-center justify-center rounded-full bg-indigo-600 px-1 text-[10px] leading-5 font-semibold text-white">{count > 99 ? '99+' : count}</span>}
      </button>

      {open && (
        <div id={panelId} role="dialog" aria-label="Bildirişlər" className="exam-notify-panel">
          <div className="flex items-center justify-between gap-2 border-b border-slate-200 px-4 py-3">
            <p className="text-sm font-semibold text-slate-900">Bildirişlər{count > 0 && <span className="ml-1.5 text-slate-500">· {count} yeni</span>}</p>
            <div className="flex items-center gap-1">
              {count > 0 && (
                <button type="button" onClick={() => read.mutate({ action: 'read' })} disabled={read.isPending}
                  className="rounded-md px-2 py-1 text-xs font-medium text-indigo-700 hover:bg-indigo-50 disabled:opacity-50">Hamısını oxunmuş et</button>
              )}
              <button type="button" onClick={() => { setOpen(false); button.current?.focus() }} aria-label="Bağla"
                className="grid size-8 place-items-center rounded-md text-slate-500 hover:bg-slate-100 hover:text-slate-900"><X className="size-4" /></button>
            </div>
          </div>
          {notifications.isLoading ? (
            <p className="px-4 py-6 text-center text-sm text-slate-500">Yüklənir…</p>
          ) : items.length === 0 ? (
            <p className="px-4 py-8 text-center text-sm text-slate-500">Yeni bildiriş yoxdur.</p>
          ) : (
            <ul className="max-h-[min(60dvh,24rem)] overflow-y-auto overscroll-contain">
              {items.map(item => (
                <li key={item.id} className={cn('flex gap-3 border-b border-slate-100 px-4 py-3 last:border-b-0', !item.isRead && 'bg-indigo-50/50')}>
                  <span className={cn('mt-1.5 size-2 shrink-0 rounded-full', item.isRead ? 'bg-transparent' : 'bg-indigo-600')} aria-hidden />
                  <div className="min-w-0 flex-1">
                    <p className="text-sm leading-5 text-slate-800">{describe(item)}{!item.isRead && <span className="sr-only"> (oxunmamış)</span>}</p>
                    <p className="mt-0.5 text-xs text-slate-500">{formatDate(item.createdAt)}</p>
                  </div>
                  <div className="flex shrink-0 items-start gap-0.5">
                    {!item.isRead && (
                      <button type="button" onClick={() => read.mutate({ id: item.id, action: 'read' })} aria-label="Oxunmuş et" title="Oxunmuş et"
                        className="grid size-8 place-items-center rounded-md text-slate-500 hover:bg-white hover:text-slate-900"><Check className="size-4" /></button>
                    )}
                    <button type="button" onClick={() => remove.mutate({ id: item.id })} aria-label="Sil" title="Sil"
                      className="grid size-8 place-items-center rounded-md text-slate-400 hover:bg-rose-50 hover:text-rose-600"><Trash2 className="size-4" /></button>
                  </div>
                </li>
              ))}
            </ul>
          )}
          <p className="border-t border-slate-200 px-4 py-2.5 text-xs text-slate-500">Dəvətlərə cavab vermək üçün imtahandan sonra Bildirişlər bölməsinə keçin.</p>
        </div>
      )}
    </div>
  )
}

function describe(n: SocialNotification) {
  switch (n.kind) {
    case 'FriendRequest': return `${n.actorName} sizə dostluq sorğusu göndərdi.`
    case 'FriendAccepted': return `${n.actorName} dostluq sorğunuzu qəbul etdi.`
    case 'BattleInvite': return `${n.actorName} sizi Battle-a dəvət etdi${n.documentName ? ` · ${n.documentName}` : ''}.`
    case 'GroupAdded': return `${n.actorName} sizi qrupa əlavə etdi.`
  }
}
