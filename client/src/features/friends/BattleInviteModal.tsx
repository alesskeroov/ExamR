import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router'
import { Alert } from '../../components/common/Alert'
import { Button } from '../../components/common/Button'
import { EmptyState } from '../../components/common/EmptyState'
import { useInviteFriend, useLobbies, type Friend } from './api'
import { useTranslation } from 'react-i18next'

export function BattleInviteModal({ friend, onClose, onSent }: { friend: Friend; onClose: () => void; onSent: () => void }) {
  const { t } = useTranslation()
  const ref = useRef<HTMLDialogElement>(null)
  const lobbies = useLobbies()
  const invite = useInviteFriend()
  const [code, setCode] = useState('')
  const selected = code || lobbies.data?.[0]?.code || ''
  useEffect(() => { const dialog = ref.current; dialog?.showModal(); return () => dialog?.close() }, [])
  return (
    <dialog ref={ref} aria-labelledby="invite-title" onCancel={onClose}
      className="m-auto max-h-[85dvh] w-[calc(100%-2rem)] max-w-md overflow-y-auto rounded-xl border border-slate-200 bg-white p-6 shadow-xl backdrop:bg-slate-900/40">
      <h2 id="invite-title" className="text-lg font-semibold">{t('profile.social.invite')}</h2>
      <p className="mt-1 text-slate-600">{t('friends.invite.text', { name: friend.displayName })}</p>
      <div className="mt-5 space-y-3">
        {lobbies.isPending && <p role="status" className="text-slate-500">{t('battle.rooms.loading')}</p>}
        {lobbies.isError && <Alert title={t('battle.rooms.loadFailed')}>{lobbies.error.message}</Alert>}
        {lobbies.data?.length === 0 && <EmptyState title={t('friends.invite.noRooms')} description={t('friends.invite.noRoomsText')}
          action={<Link to="/battle" className="font-medium text-indigo-600">{t('profile.activity.goBattle')}</Link>} />}
        {lobbies.data && lobbies.data.length > 0 && <fieldset className="space-y-2"><legend className="sr-only">{t('friends.invite.openRooms')}</legend>
          {lobbies.data.map(lobby => <label key={lobby.code} className="flex cursor-pointer items-start gap-3 rounded-xl border border-slate-200 p-4 has-checked:border-indigo-300 has-checked:bg-indigo-50/50">
            <input type="radio" name="lobby" value={lobby.code} checked={selected === lobby.code} onChange={() => setCode(lobby.code)} className="mt-1 accent-indigo-600" />
            <span className="min-w-0"><span className="block font-medium break-words">{lobby.documentName}</span>
              <span className="mt-1 block text-xs text-slate-500">{lobby.code} · {t('common.questions', { count: lobby.questionCount })} · {lobby.hostName}</span></span>
          </label>)}
        </fieldset>}
        {invite.isError && <Alert title={t('battle.invite.failed')}>{invite.error.message}</Alert>}
        <div className="flex justify-end gap-2 pt-2">
          <Button variant="secondary" onClick={onClose}>{t('common.close')}</Button>
          <Button disabled={!lobbies.data?.some(l => l.code === selected)} loading={invite.isPending}
            onClick={() => invite.mutate({ id: friend.id, roomCode: selected }, { onSuccess: onSent })}>{t('friends.invite.send')}</Button>
        </div>
      </div>
    </dialog>
  )
}
