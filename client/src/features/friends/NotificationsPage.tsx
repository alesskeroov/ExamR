import { useDeleteNotifications } from './notification-management'
import { useConfirm } from '../../lib/use-confirm'
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
  const remove = useDeleteNotifications()
  const confirm = useConfirm()
  async function deleteItems(id?: string, readOnly = false) {
    if (await confirm({ title: id ? 'Bildiriş silinsin?' : readOnly ? 'Oxunmuş bildirişlər silinsin?' : 'Bütün bildirişlər silinsin?', description: 'Yalnız sizin bildirişləriniz silinəcək. Dostluqlar və Battle nəticələri dəyişməyəcək.', confirmLabel: 'Sil', danger: true })) remove.mutate({ id, readOnly })
  }
  const navigate = useNavigate()
  const notifications = useNotifications()
  const action = useNotificationAction()
  const friendAction = useFriendAction()
  const busy = action.isPending || friendAction.isPending || remove.isPending
  const error = action.error ?? friendAction.error ?? remove.error
  return <>
    <PageHeader title="Bildirişlər" description="Dostluq, Battle və qrup bildirişləri. Son 100 bildiriş göstərilir." />
    <div className="mb-5 flex flex-wrap items-center gap-3">
      <p className="text-slate-500">{notifications.data?.unreadCount ?? 0} oxunmamış bildiriş</p>
      <Button variant="secondary" disabled={busy || !notifications.data?.unreadCount} onClick={() => action.mutate({ action: 'read' })}>Hamısını oxunmuş et</Button>
      <Button variant="ghost" disabled={busy || !notifications.data?.items.length} onClick={() => void deleteItems(undefined, true)}>Oxunmuşları sil</Button>
      <Button variant="ghost" disabled={busy || !notifications.data?.items.length} onClick={() => void deleteItems()}>Hamısını sil</Button>
    </div>
    {remove.isSuccess && <div role="status" className="mb-4"><Alert tone="info" title="Bildirişlər silindi." /></div>}
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
          <Button variant="ghost" disabled={busy} onClick={() => void deleteItems(n.id)}>Bildirişi sil</Button>
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
