import { Bell } from 'lucide-react'
import { Link } from 'react-router'
import { useNotifications } from './api'

export function NotificationBell() {
  const notifications = useNotifications()
  const count = notifications.data?.unreadCount ?? 0
  return (
    <Link to="/notifications" aria-label={`Bildirişlər${count ? `, ${count} oxunmamış` : ''}`}
      className="relative inline-flex size-10 shrink-0 items-center justify-center rounded-lg text-slate-600 hover:bg-slate-100">
      <Bell className="size-5" aria-hidden />
      {count > 0 && <span aria-hidden className="absolute -top-0.5 -right-0.5 flex min-w-5 items-center justify-center rounded-full bg-indigo-600 px-1 text-[10px] leading-5 font-semibold text-white">{count > 99 ? '99+' : count}</span>}
    </Link>
  )
}
