import { Crown, DoorOpen, Users } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router'
import { Alert } from '../../components/common/Alert'
import { Button } from '../../components/common/Button'
import { EmptyState } from '../../components/common/EmptyState'
import { PageHeader } from '../../components/common/PageHeader'
import { useConfirm } from '../../lib/use-confirm'
import { cn } from '../../lib/utils'
import { useCloseRoom, useMyRooms } from './api'

const statuses = { Waiting: 'Gözləyir', InProgress: 'Yarış davam edir', Completed: 'Tamamlanıb' }

export function MyRoomsPage() {
  const [completed, setCompleted] = useState(false)
  const rooms = useMyRooms(completed)
  const close = useCloseRoom()
  const confirm = useConfirm()
  return <>
    <PageHeader title="Otaqlarım" description="Yaratdığınız və üzv olduğunuz otaqlar. Boş qalan otaqlar avtomatik silinir." />
    <div className="mb-5 flex flex-wrap items-center justify-between gap-3">
      <label className="flex items-center gap-2 text-slate-600"><input type="checkbox" checked={completed} onChange={e => setCompleted(e.target.checked)} className="accent-indigo-600" />Tamamlanmış otaqları da göstər</label>
      <Link to="/battle" className="font-medium text-indigo-600 hover:text-indigo-700">Yeni otaq yarat</Link>
    </div>
    {close.isError && <div className="mb-4"><Alert title="Otaq bağlanmadı">{close.error.message}</Alert></div>}
    {rooms.isPending && <p role="status" className="py-8 text-slate-500">Otaqlar yüklənir…</p>}
    {rooms.isError && <Alert title="Otaqlar yüklənmədi">{rooms.error.message}</Alert>}
    {rooms.data?.length === 0 && <EmptyState title="Hələ otaq yoxdur" description="Battle otağı yaradın və ya dostunuzun otağına qoşulun." icon={<DoorOpen className="size-6" />} />}
    <div className="grid gap-4 md:grid-cols-2">{rooms.data?.map(room => <article key={room.id} className="flex flex-col rounded-xl border border-slate-200 bg-white p-5">
      <div className="flex items-start justify-between gap-3"><h2 className="min-w-0 text-base font-semibold break-words">{room.name}</h2>
        <span className={cn('shrink-0 rounded-md px-2 py-1 text-xs font-medium', room.status === 'Waiting' ? 'bg-slate-100 text-slate-600' : room.status === 'InProgress' ? 'bg-indigo-50 text-indigo-700' : 'bg-emerald-50 text-emerald-700')}>
          {room.isClosed ? 'Bağlanıb' : statuses[room.status]}</span>
      </div>
      <p className="mt-3 text-sm break-words text-slate-600">{room.documentName}</p>
      <div className="mt-3 flex flex-wrap items-center gap-4 text-xs text-slate-500"><span className="font-mono tracking-wider">{room.code}</span>
        <span className="flex items-center gap-1"><Users className="size-3.5" aria-hidden />{room.participantCount}/10</span>
        {room.isHost && <span className="flex items-center gap-1"><Crown className="size-3.5" aria-hidden />Host</span>}
      </div>
      <div className="mt-auto flex items-center justify-between gap-2 pt-5">
        {room.canJoin ? <Link to={`/battle/${room.code}`} className="rounded-lg border border-slate-300 px-4 py-2 font-medium text-slate-700 hover:bg-slate-50">Otağa daxil ol</Link>
          : <span className="text-xs text-slate-500">Otağa giriş mümkün deyil</span>}
        {room.isHost && !room.isClosed && <Button variant="ghost" disabled={close.isPending} onClick={async () => {
          if (await confirm({ title: 'Otaq bağlansın?', description: `“${room.name}” bütün iştirakçılar üçün bağlanacaq.`, confirmLabel: 'Otağı bağla', danger: true })) close.mutate(room.code)
        }}>Otağı bağla</Button>}
      </div>
    </article>)}</div>
  </>
}
