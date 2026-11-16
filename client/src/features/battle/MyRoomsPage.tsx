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
import { useTranslation } from 'react-i18next'


export function MyRoomsPage() {
  const { t } = useTranslation()
  const [completed, setCompleted] = useState(false)
  const rooms = useMyRooms(completed)
  const close = useCloseRoom()
  const confirm = useConfirm()
  return <>
    <PageHeader title={t('nav.items.rooms')} description={t('battle.rooms.description')} />
    <div className="mb-5 flex flex-wrap items-center justify-between gap-3">
      <label className="flex items-center gap-2 text-slate-600"><input type="checkbox" checked={completed} onChange={e => setCompleted(e.target.checked)} className="accent-indigo-600" />{t('battle.rooms.showCompleted')}</label>
      <Link to="/battle" className="font-medium text-indigo-600 hover:text-indigo-700">{t('battle.rooms.create')}</Link>
    </div>
    {close.isError && <div className="mb-4"><Alert title={t('battle.rooms.closeFailed')}>{close.error.message}</Alert></div>}
    {rooms.isPending && <p role="status" className="py-8 text-slate-500">{t('battle.rooms.loading')}</p>}
    {rooms.isError && <Alert title={t('battle.rooms.loadFailed')}>{rooms.error.message}</Alert>}
    {rooms.data?.length === 0 && <EmptyState title={t('battle.rooms.empty')} description={t('battle.rooms.emptyText')} icon={<DoorOpen className="size-6" />} />}
    <div className="grid gap-4 md:grid-cols-2">{rooms.data?.map(room => <article key={room.id} className="flex flex-col rounded-xl border border-slate-200 bg-white p-5">
      <div className="flex items-start justify-between gap-3"><h2 className="min-w-0 text-base font-semibold break-words">{room.name}</h2>
        <span className={cn('shrink-0 rounded-md px-2 py-1 text-xs font-medium', room.status === 'Waiting' ? 'bg-slate-100 text-slate-600' : room.status === 'InProgress' ? 'bg-indigo-50 text-indigo-700' : 'bg-emerald-50 text-emerald-700')}>
          {room.isClosed ? t('battle.rooms.closed') : t(`battle.rooms.status.${room.status}`)}</span>
      </div>
      <p className="mt-3 text-sm break-words text-slate-600">{room.documentName}</p>
      <div className="mt-3 flex flex-wrap items-center gap-4 text-xs text-slate-500"><span className="font-mono tracking-wider">{room.code}</span>
        <span className="flex items-center gap-1"><Users className="size-3.5" aria-hidden />{room.participantCount}/10</span>
        {room.isHost && <span className="flex items-center gap-1"><Crown className="size-3.5" aria-hidden />{t('battle.host')}</span>}
      </div>
      <div className="mt-auto flex items-center justify-between gap-2 pt-5">
        {room.canJoin ? <Link to={`/battle/${room.code}`} className="rounded-lg border border-slate-300 px-4 py-2 font-medium text-slate-700 hover:bg-slate-50">{t('battle.rooms.enter')}</Link>
          : <span className="text-xs text-slate-500">{t('battle.rooms.noEntry')}</span>}
        {room.isHost && !room.isClosed && <Button variant="ghost" disabled={close.isPending} onClick={async () => {
          if (await confirm({ title: t('battle.rooms.closeTitle'), description: t('battle.rooms.closeText', { name: room.name }), confirmLabel: t('battle.rooms.close'), danger: true })) close.mutate(room.code)
        }}>{t('battle.rooms.close')}</Button>}
      </div>
    </article>)}</div>
  </>
}
