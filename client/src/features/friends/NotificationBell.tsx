import { Bell, Check, Trash2, X } from 'lucide-react'
import { useEffect, useId, useRef, useState, type ButtonHTMLAttributes, type CSSProperties } from 'react'
import { Link, useNavigate } from 'react-router'
import { cn } from '../../lib/utils'
import { useFriendAction, useNotificationAction, useNotifications, type SocialNotification } from './api'
import { useDeleteNotifications } from './notification-management'
import './notifications.css'
import { useTranslation } from 'react-i18next'
import i18n, { localeTag } from '../../lib/i18n'

interface Props {
  /** Exam / live battle: the panel never offers actions that leave the current screen. */
  safe?: boolean
  poll?: boolean
}

/**
 * Bell with an in-place panel (latest notifications, quick actions) instead of a separate page; the full history
 * stays at /notifications behind "Hamısına bax".
 */
export function NotificationBell({ safe = false, poll = true }: Props) {
  const notifications = useNotifications(poll)
  const read = useNotificationAction()
  const friend = useFriendAction()
  const remove = useDeleteNotifications()
  const navigate = useNavigate()
  const [open, setOpen] = useState(false)
  const [sheetTop, setSheetTop] = useState<number | null>(null)
  const root = useRef<HTMLDivElement>(null)
  const button = useRef<HTMLButtonElement>(null)
  const panelId = useId()
  const { t } = useTranslation()
  const count = notifications.data?.unreadCount ?? 0
  const items = (notifications.data?.items ?? []).slice(0, 8)
  const busy = read.isPending || friend.isPending || remove.isPending
  const error = read.error ?? friend.error ?? remove.error

  const close = (focusButton = false) => {
    setOpen(false)
    if (focusButton) button.current?.focus()
  }

  const toggle = () => {
    // On phones the panel is a sheet just under the header the bell lives in.
    const rect = button.current?.getBoundingClientRect()
    setSheetTop(rect ? Math.round(rect.bottom + 8) : null)
    setOpen(value => !value)
  }

  useEffect(() => {
    if (!open) return
    const onPointer = (event: PointerEvent) => { if (!root.current?.contains(event.target as Node)) setOpen(false) }
    const onKey = (event: KeyboardEvent) => {
      if (event.key !== 'Escape') return
      event.stopPropagation()
      close(true)
    }
    document.addEventListener('pointerdown', onPointer)
    document.addEventListener('keydown', onKey, true)
    return () => { document.removeEventListener('pointerdown', onPointer); document.removeEventListener('keydown', onKey, true) }
  }, [open])

  const go = (path: string) => { close(); navigate(path) }

  return (
    <div ref={root} className="notify-root relative shrink-0">
      <button ref={button} type="button" onClick={toggle} aria-expanded={open} aria-controls={panelId} aria-haspopup="dialog"
        aria-label={count ? t('notifications.bellUnread', { count }) : t('notifications.title')}
        className={cn('relative grid size-10 place-items-center rounded-lg text-slate-600 transition-colors duration-150 hover:bg-slate-100 hover:text-slate-900', open && 'bg-slate-100 text-slate-900')}>
        <Bell className="size-5" aria-hidden />
        {count > 0 && <span aria-hidden className="absolute -top-0.5 -right-0.5 flex min-w-5 items-center justify-center rounded-full bg-indigo-600 px-1 text-[10px] leading-5 font-semibold text-white">{count > 99 ? '99+' : count}</span>}
      </button>

      {open && (
        <div id={panelId} role="dialog" aria-label={t('notifications.title')} className="notify-panel" style={{ '--notify-sheet-top': sheetTop ? `${sheetTop}px` : undefined } as CSSProperties}>
          <div className="flex items-center justify-between gap-2 border-b border-slate-200 px-4 py-3">
            <p className="text-sm font-semibold text-slate-900">{t('notifications.title')}{count > 0 && <span className="ml-1.5 font-normal text-slate-500">· {t('notifications.newCount', { count })}</span>}</p>
            <div className="flex items-center gap-1">
              {count > 0 && (
                <button type="button" onClick={() => read.mutate({ action: 'read' })} disabled={busy}
                  className="rounded-md px-2 py-1 text-xs font-medium text-indigo-700 hover:bg-indigo-50 disabled:opacity-50">{t('notifications.markAllRead')}</button>
              )}
              <button type="button" onClick={() => close(true)} aria-label={t('common.close')}
                className="grid size-8 place-items-center rounded-md text-slate-500 hover:bg-slate-100 hover:text-slate-900"><X className="size-4" /></button>
            </div>
          </div>

          {error && <p role="alert" className="border-b border-slate-200 bg-rose-50 px-4 py-2 text-xs text-rose-700">{error.message}</p>}

          {notifications.isLoading ? (
            <p className="px-4 py-6 text-center text-sm text-slate-500">{t('common.loadingDots')}</p>
          ) : items.length === 0 ? (
            <div className="px-4 py-10 text-center">
              <Bell className="mx-auto size-6 text-slate-300" aria-hidden />
              <p className="mt-2 text-sm text-slate-500">{t('notifications.emptyShort')}</p>
            </div>
          ) : (
            <ul className="notify-list">
              {items.map(item => {
                const pending = item.status === 'Pending'
                return (
                  <li key={item.id} className={cn('flex gap-3 border-b border-slate-100 px-4 py-3 last:border-b-0', !item.isRead && 'bg-indigo-50/50')}>
                    <span className={cn('mt-1.5 size-2 shrink-0 rounded-full', item.isRead ? 'bg-transparent' : 'bg-indigo-600')} aria-hidden />
                    <div className="min-w-0 flex-1">
                      <p className="text-sm leading-5 break-words text-slate-800">{describe(item)}{!item.isRead && <span className="sr-only"> ({t('notifications.unread')})</span>}</p>
                      <p className="mt-0.5 text-xs text-slate-500">{relativeTime(item.createdAt)}</p>
                      {!safe && pending && item.kind === 'FriendRequest' && (
                        <div className="mt-2 flex gap-2">
                          <QuickButton primary disabled={busy} onClick={() => friend.mutate({ id: item.actorId, action: 'accept' })}>{t('notifications.accept')}</QuickButton>
                          <QuickButton disabled={busy} onClick={() => friend.mutate({ id: item.actorId, action: 'reject' })}>{t('notifications.reject')}</QuickButton>
                        </div>
                      )}
                      {!safe && pending && item.kind === 'BattleInvite' && (
                        <div className="mt-2 flex gap-2">
                          <QuickButton primary disabled={busy} onClick={() => read.mutate({ id: item.id, action: 'accept' }, { onSuccess: result => { if (result?.code) go(`/battle/${result.code}`) } })}>{t('notifications.join')}</QuickButton>
                          <QuickButton disabled={busy} onClick={() => read.mutate({ id: item.id, action: 'reject' })}>{t('notifications.reject')}</QuickButton>
                        </div>
                      )}
                      {!safe && item.kind === 'GroupAdded' && (
                        <div className="mt-2"><QuickButton onClick={() => go('/groups')}>{t('notifications.viewGroups')}</QuickButton></div>
                      )}
                    </div>
                    <div className="flex shrink-0 items-start gap-0.5">
                      {!item.isRead && (
                        <button type="button" onClick={() => read.mutate({ id: item.id, action: 'read' })} disabled={busy} aria-label={t('notifications.markRead')} title={t('notifications.markRead')}
                          className="grid size-8 place-items-center rounded-md text-slate-500 hover:bg-white hover:text-slate-900"><Check className="size-4" /></button>
                      )}
                      <button type="button" onClick={() => remove.mutate({ id: item.id })} disabled={busy} aria-label={t('notifications.delete')} title={t('common.delete')}
                        className="grid size-8 place-items-center rounded-md text-slate-400 hover:bg-rose-50 hover:text-rose-600"><Trash2 className="size-4" /></button>
                    </div>
                  </li>
                )
              })}
            </ul>
          )}

          {safe ? (
            <p className="border-t border-slate-200 px-4 py-2.5 text-xs text-slate-500">{t('notifications.safeHint')}</p>
          ) : (
            <Link to="/notifications" onClick={() => close()} className="block border-t border-slate-200 px-4 py-2.5 text-center text-sm font-medium text-indigo-700 hover:bg-slate-50">
              {t('notifications.viewAll')}
            </Link>
          )}
        </div>
      )}
    </div>
  )
}

function QuickButton({ primary, ...props }: ButtonHTMLAttributes<HTMLButtonElement> & { primary?: boolean }) {
  return (
    <button type="button" {...props}
      className={cn('inline-flex h-8 items-center rounded-lg px-3 text-xs font-semibold transition-colors duration-150 disabled:opacity-50',
        primary ? 'btn-primary' : 'border border-slate-300 bg-white text-slate-700 hover:bg-slate-50')} />
  )
}

function describe(n: SocialNotification) {
  switch (n.kind) {
    case 'FriendRequest': return i18n.t('notifications.describe.friendRequest', { name: n.actorName })
    case 'FriendAccepted': return i18n.t('notifications.describe.friendAccepted', { name: n.actorName })
    case 'BattleInvite': return n.documentName ? i18n.t('notifications.describe.battleInviteDoc', { name: n.actorName, doc: n.documentName }) : i18n.t('notifications.describe.battleInvite', { name: n.actorName })
    case 'GroupAdded': return n.documentName ? i18n.t('notifications.describe.groupAddedNamed', { name: n.actorName, group: n.documentName }) : i18n.t('notifications.describe.groupAdded', { name: n.actorName })
  }
}

function relativeTime(iso: string) {
  const minutes = Math.round((Date.now() - Date.parse(iso)) / 60_000)
  if (minutes < 1) return i18n.t('time.justNow')
  if (minutes < 60) return i18n.t('time.minutesAgo', { count: minutes })
  const hours = Math.round(minutes / 60)
  if (hours < 24) return i18n.t('time.hoursAgo', { count: hours })
  const days = Math.round(hours / 24)
  return days < 7 ? i18n.t('time.daysAgo', { count: days }) : new Date(iso).toLocaleDateString(localeTag())
}
