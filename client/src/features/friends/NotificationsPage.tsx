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
import { useTranslation } from 'react-i18next'
import { localeTag } from '../../lib/i18n'

const statusLabels = ['Accepted', 'Rejected', 'Expired', 'Unavailable']

export function NotificationsPage() {
  const remove = useDeleteNotifications()
  const confirm = useConfirm()
  const { t } = useTranslation()
  async function deleteItems(id?: string, readOnly = false) {
    if (await confirm({ title: id ? t('notifications.confirmOne') : readOnly ? t('notifications.confirmRead') : t('notifications.confirmAll'), description: t('notifications.confirmText'), confirmLabel: t('common.delete'), danger: true })) remove.mutate({ id, readOnly })
  }
  const navigate = useNavigate()
  const notifications = useNotifications()
  const action = useNotificationAction()
  const friendAction = useFriendAction()
  const busy = action.isPending || friendAction.isPending || remove.isPending
  const error = action.error ?? friendAction.error ?? remove.error
  return <>
    <PageHeader title={t('notifications.title')} description={t('notifications.description')} />
    <div className="mb-5 flex flex-wrap items-center gap-3">
      <p className="text-slate-500">{t('notifications.unreadCount', { count: notifications.data?.unreadCount ?? 0 })}</p>
      <Button variant="secondary" disabled={busy || !notifications.data?.unreadCount} onClick={() => action.mutate({ action: 'read' })}>{t('notifications.markAllRead')}</Button>
      <Button variant="ghost" disabled={busy || !notifications.data?.items.length} onClick={() => void deleteItems(undefined, true)}>{t('notifications.deleteRead')}</Button>
      <Button variant="ghost" disabled={busy || !notifications.data?.items.length} onClick={() => void deleteItems()}>{t('notifications.deleteAll')}</Button>
    </div>
    {remove.isSuccess && <div role="status" className="mb-4"><Alert tone="info" title={t('notifications.deleted')} /></div>}
    {error && <div className="mb-4"><Alert title={t('profile.actionFailed')}>{error.message}</Alert></div>}
    {notifications.isPending && <p role="status" className="py-8 text-slate-500">{t('notifications.loading')}</p>}
    {notifications.isError && <Alert title={t('notifications.loadFailed')}>{notifications.error.message}</Alert>}
    {notifications.data?.items.length === 0 && <EmptyState title={t('notifications.empty')} description={t('notifications.emptyText')} icon={<Bell className="size-6" />} />}
    <ul className="space-y-3">{notifications.data?.items.map(n => {
      const pending = n.status === 'Pending'
      return <li key={n.id} className={cn('rounded-xl border bg-white p-5', n.isRead ? 'border-slate-200' : 'border-indigo-200')}>
        <div className="flex items-start justify-between gap-3">
          <h2 className="font-medium break-words">{n.kind === 'FriendRequest' ? t('notifications.page.friendRequest', { name: n.actorName }) : n.kind === 'FriendAccepted' ? t('notifications.page.friendAccepted', { name: n.actorName }) : n.kind === 'GroupAdded' ? t('notifications.page.groupAdded', { name: n.actorName, group: n.documentName }) : t('notifications.page.battleInvite', { name: n.actorName })}</h2>
          {!n.isRead && <span className="mt-1.5 size-2 shrink-0 rounded-full bg-indigo-500" aria-label={t('notifications.unreadLabel')} />}
        </div>
        <p className="mt-1 text-xs text-slate-500">{new Date(n.createdAt).toLocaleString(localeTag())}</p>
        {n.kind === 'BattleInvite' && <div className="mt-3 rounded-lg bg-slate-50 px-3 py-3 text-slate-600">
          <p className="font-medium break-words text-slate-800">{n.documentName}</p>
          <p className="mt-1 text-xs">{t('notifications.host', { name: n.hostName })} · {t('common.questions', { count: n.questionCount })} · {n.roomCode}</p>
          {n.expiresAt && <p className="mt-1 text-xs">{t('notifications.deadline', { time: new Date(n.expiresAt).toLocaleTimeString(localeTag(), { hour: '2-digit', minute: '2-digit' }) })}</p>}
        </div>}
        <div className="mt-4 flex flex-wrap items-center gap-2">
          <Button variant="ghost" disabled={busy} onClick={() => void deleteItems(n.id)}>{t('notifications.delete')}</Button>
          {pending && n.kind === 'FriendRequest' && <>
            <Button disabled={busy} onClick={() => friendAction.mutate({ id: n.actorId, action: 'accept' })}>{t('notifications.accept')}</Button>
            <Button variant="secondary" disabled={busy} onClick={() => friendAction.mutate({ id: n.actorId, action: 'reject' })}>{t('notifications.reject')}</Button>
          </>}
          {pending && n.kind === 'BattleInvite' && <>
            <Button disabled={busy} onClick={() => action.mutate({ id: n.id, action: 'accept' }, { onSuccess: result => { if (result?.code) navigate(`/battle/${result.code}`) } })}>{t('notifications.joinBattle')}</Button>
            <Button variant="secondary" disabled={busy} onClick={() => action.mutate({ id: n.id, action: 'reject' })}>{t('notifications.reject')}</Button>
          </>}
          {n.kind === 'GroupAdded' && <Button variant="secondary" onClick={() => navigate('/groups')}>{t('notifications.viewGroups')}</Button>}
          {!pending && n.kind !== 'GroupAdded' && <span className="text-xs text-slate-500">{t(`notifications.status.${statusLabels.includes(n.status) ? n.status : 'Expired'}`)}</span>}
          {!n.isRead && <Button variant="ghost" disabled={busy} onClick={() => action.mutate({ id: n.id, action: 'read' })}>{t('notifications.markRead')}</Button>}
        </div>
      </li>
    })}</ul>
  </>
}
