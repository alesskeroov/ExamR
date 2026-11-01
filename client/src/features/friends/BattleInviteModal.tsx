import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router'
import { Alert } from '../../components/common/Alert'
import { Button } from '../../components/common/Button'
import { EmptyState } from '../../components/common/EmptyState'
import { useInviteFriend, useLobbies, type Friend } from './api'

export function BattleInviteModal({ friend, onClose, onSent }: { friend: Friend; onClose: () => void; onSent: () => void }) {
  const ref = useRef<HTMLDialogElement>(null)
  const lobbies = useLobbies()
  const invite = useInviteFriend()
  const [code, setCode] = useState('')
  const selected = code || lobbies.data?.[0]?.code || ''
  useEffect(() => { const dialog = ref.current; dialog?.showModal(); return () => dialog?.close() }, [])
  return (
    <dialog ref={ref} aria-labelledby="invite-title" onCancel={onClose}
      className="m-auto max-h-[85dvh] w-[calc(100%-2rem)] max-w-md overflow-y-auto rounded-xl border border-slate-200 bg-white p-6 shadow-xl backdrop:bg-slate-900/40">
      <h2 id="invite-title" className="text-lg font-semibold">Battle-a dəvət et</h2>
      <p className="mt-1 text-slate-600">{friend.displayName} üçün açıq otaq seçin. Dəvət oflayn olduqda da saxlanılır.</p>
      <div className="mt-5 space-y-3">
        {lobbies.isPending && <p role="status" className="text-slate-500">Otaqlar yüklənir…</p>}
        {lobbies.isError && <Alert title="Otaqlar yüklənmədi">{lobbies.error.message}</Alert>}
        {lobbies.data?.length === 0 && <EmptyState title="Açıq otağınız yoxdur" description="Əvvəlcə Battle otağı yaradın və ya lobby-yə qoşulun."
          action={<Link to="/battle" className="font-medium text-indigo-600">Battle-a keç</Link>} />}
        {lobbies.data && lobbies.data.length > 0 && <fieldset className="space-y-2"><legend className="sr-only">Açıq otaqlar</legend>
          {lobbies.data.map(lobby => <label key={lobby.code} className="flex cursor-pointer items-start gap-3 rounded-xl border border-slate-200 p-4 has-checked:border-indigo-300 has-checked:bg-indigo-50/50">
            <input type="radio" name="lobby" value={lobby.code} checked={selected === lobby.code} onChange={() => setCode(lobby.code)} className="mt-1 accent-indigo-600" />
            <span className="min-w-0"><span className="block font-medium break-words">{lobby.documentName}</span>
              <span className="mt-1 block text-xs text-slate-500">{lobby.code} · {lobby.questionCount} sual · {lobby.hostName}</span></span>
          </label>)}
        </fieldset>}
        {invite.isError && <Alert title="Dəvət göndərilmədi">{invite.error.message}</Alert>}
        <div className="flex justify-end gap-2 pt-2">
          <Button variant="secondary" onClick={onClose}>Bağla</Button>
          <Button disabled={!lobbies.data?.some(l => l.code === selected)} loading={invite.isPending}
            onClick={() => invite.mutate({ id: friend.id, roomCode: selected }, { onSuccess: onSent })}>Dəvət göndər</Button>
        </div>
      </div>
    </dialog>
  )
}
