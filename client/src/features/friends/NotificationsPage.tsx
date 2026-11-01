import { Bell } from 'lucide-react'
import { useNavigate } from 'react-router'
import { Alert } from '../../components/common/Alert'
import { Button } from '../../components/common/Button'
import { EmptyState } from '../../components/common/EmptyState'
import { PageHeader } from '../../components/common/PageHeader'
import { cn } from '../../lib/utils'
import { useFriendAction, useNotificationAction, useNotifications } from './api'

const statusLabels: Record<string, string> = { Accepted: 'Qəbul edilib', Rejected: 'Rədd edilib', Expired: 'Vaxtı bitib', Unavailable: 'Artıq aktiv deyil' }

export function NotificationsPage() {
  const navigate = useNavigate()
  const notifications = useNotifications()
  const action = useNotificationAction()
  const friendAction = useFriendAction()
  const busy = action.isPending || friendAction.isPending
  const error = action.error ?? friendAction.error
  return <>
    <PageHeader title="Bildirişlər" description="Dostluq, Battle və qrup bildirişləri. Son 100 bildiriş göstərilir." />
    <div className="mb-5 flex items-center justify-between gap-3">
      <p className="text-slate-500">{notifications.data?.unreadCount ?? 0} oxunmamış bildiriş</p>
      <Button variant="secondary" disabled={busy || !notifications.data?.unreadCount} onClick={() => action.mutate({ action: 'read' })}>Hamısını oxunmuş et</Button>
    </div>
    {error && <div className="mb-4"><Alert title="Əməliyyat alınmadı">{error.message}</Alert></div>}
    {notifications.isPending && <p role="status" className="py-8 text-slate-500">Bildirişlər yüklənir…</p>}
    {notifications.isError && <Alert title="Bildirişlər yüklənmədi">{notifications.error.message}</Alert>}
    {notifications.data?.items.length === 0 && <EmptyState title="Hələ bildiriş yoxdur" description="Yeni sorğu və dəvətlər burada görünəcək." icon={<Bell className="size-6" />} />}
    <ul className="space-y-3">{notifications.data?.items.map(n => {
      const pending = n.status === 'Pending'
      return <li key={n.id} className={cn('rounded-xl border bg-white p-5', n.isRead ? 'border-slate-200' : 'border-indigo-200')}>
        <div className="flex items-start justify-between gap-3">
          <h2 className="font-medium break-words">{n.actorName} {n.kind === 'FriendRequest' ? 'dostluq sorğusu göndərdi' : n.kind === 'FriendAccepted' ? 'dostluq sorğunuzu qəbul etdi' : n.kind === 'GroupAdded' ? `sizi “${n.documentName}” qrupuna əlavə etdi` : 'sizi Battle-a dəvət edir'}</h2>
          {!n.isRead && <span className="mt-1.5 size-2 shrink-0 rounded-full bg-indigo-500" aria-label="Oxunmamış" />}
        </div>
        <p className="mt-1 text-xs text-slate-500">{new Date(n.createdAt).toLocaleString('az-AZ')}</p>
        {n.kind === 'BattleInvite' && <div className="mt-3 rounded-lg bg-slate-50 px-3 py-3 text-slate-600">
          <p className="font-medium break-words text-slate-800">{n.documentName}</p>
          <p className="mt-1 text-xs">Otaq sahibi: {n.hostName} · {n.questionCount} sual · {n.roomCode}</p>
          {n.expiresAt && <p className="mt-1 text-xs">Son vaxt: {new Date(n.expiresAt).toLocaleTimeString('az-AZ', { hour: '2-digit', minute: '2-digit' })}</p>}
        </div>}
        <div className="mt-4 flex flex-wrap items-center gap-2">
          {pending && n.kind === 'FriendRequest' && <>
            <Button disabled={busy} onClick={() => friendAction.mutate({ id: n.actorId, action: 'accept' })}>Qəbul et</Button>
            <Button variant="secondary" disabled={busy} onClick={() => friendAction.mutate({ id: n.actorId, action: 'reject' })}>Rədd et</Button>
          </>}
          {pending && n.kind === 'BattleInvite' && <>
            <Button disabled={busy} onClick={() => action.mutate({ id: n.id, action: 'accept' }, { onSuccess: result => { if (result?.code) navigate(`/battle/${result.code}`) } })}>Battle-a qoşul</Button>
            <Button variant="secondary" disabled={busy} onClick={() => action.mutate({ id: n.id, action: 'reject' })}>Rədd et</Button>
          </>}
          {n.kind === 'GroupAdded' && <Button variant="secondary" onClick={() => navigate('/groups')}>Qruplarıma bax</Button>}
          {!pending && n.kind !== 'GroupAdded' && <span className="text-xs text-slate-500">{statusLabels[n.status] ?? 'Vaxtı bitib'}</span>}
          {!n.isRead && <Button variant="ghost" disabled={busy} onClick={() => action.mutate({ id: n.id, action: 'read' })}>Oxunmuş et</Button>}
        </div>
      </li>
    })}</ul>
  </>
}
