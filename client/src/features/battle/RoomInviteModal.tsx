import { useEffect, useRef, useState } from 'react'
import { Alert } from '../../components/common/Alert'
import { Button } from '../../components/common/Button'
import { EmptyState } from '../../components/common/EmptyState'
import { cn } from '../../lib/utils'
import { useInviteFriend, useRoomFriends } from '../friends/api'
import { useTranslation } from 'react-i18next'

export function RoomInviteModal({ code, isFull, onClose }: { code: string; isFull: boolean; onClose: () => void }) {
  const { t } = useTranslation()
  const ref = useRef<HTMLDialogElement>(null)
  const [term, setTerm] = useState('')
  const friends = useRoomFriends(code, term.trim())
  const invite = useInviteFriend(true)
  useEffect(() => { const dialog = ref.current; dialog?.showModal(); return () => dialog?.close() }, [])
  return <dialog ref={ref} onCancel={onClose} aria-labelledby="room-invite-title"
    className="m-auto max-h-[85dvh] w-[calc(100%-2rem)] max-w-md overflow-y-auto rounded-xl border border-slate-200 bg-white p-5 shadow-xl backdrop:bg-slate-900/40">
    <div className="flex items-center justify-between gap-3">
      <h2 id="room-invite-title" className="text-lg font-semibold">{t('battle.invite.title')}</h2>
      <Button variant="ghost" className="h-8 px-2 text-xs" onClick={onClose}>{t('common.close')}</Button>
    </div>
    <p className="mt-1 text-sm text-slate-500">{t('battle.invite.text', { code })}</p>
    <label htmlFor="room-friend-search" className="sr-only">{t('battle.invite.searchLabel')}</label>
    <input id="room-friend-search" placeholder={t('battle.invite.searchPlaceholder')} value={term} maxLength={100} onChange={e => setTerm(e.target.value)}
      className="mt-4 h-10 w-full rounded-lg border border-slate-300 px-3" />
    <div className="mt-4 space-y-3">
      {isFull && <Alert title={t('battle.invite.full')}>{t('battle.invite.fullText')}</Alert>}
      {friends.isPending && <p role="status" className="py-6 text-center text-slate-500">{t('profile.social.loading')}</p>}
      {friends.isError && <Alert title={t('profile.social.failed')}>{friends.error.message}</Alert>}
      {invite.isError && <Alert title={t('battle.invite.failed')}>{invite.error.message}</Alert>}
      {friends.data?.length === 0 && <EmptyState title={t('battle.invite.noFriends')} description={t('battle.invite.noFriendsText')} />}
      <ul className="space-y-2">{friends.data?.map(friend => <li key={friend.id}
        className={cn('flex items-center gap-3 rounded-lg border border-slate-200 px-3 py-3', !friend.isOnline && 'bg-slate-50 text-slate-500 opacity-60')}>
        <div className="min-w-0 flex-1"><p className="font-medium break-words">{friend.displayName}</p>
          <p className="mt-1 flex items-center gap-1.5 text-xs text-slate-500"><span aria-hidden className={cn('size-1.5 rounded-full', friend.isOnline ? 'bg-emerald-500' : 'bg-slate-400')} />{friend.isOnline ? t('common.online') : t('common.offline')}</p>
        </div>
        {friend.isInRoom ? <span className="text-xs text-slate-500">{t('battle.invite.inRoom')}</span>
          : friend.invitationSent ? <span role="status" className="text-xs font-medium text-indigo-600">{t('battle.invite.sent')}</span>
            : <Button variant="secondary" className="h-8 shrink-0 px-3 text-xs" disabled={!friend.isOnline || isFull || invite.isPending}
              onClick={() => invite.mutate({ id: friend.id, roomCode: code })}>{t('battle.invite.invite')}</Button>}
      </li>)}</ul>
    </div>
  </dialog>
}
