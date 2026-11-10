import { LockKeyhole, Pause, Play } from 'lucide-react'
import { useState } from 'react'
import { Alert } from '../../components/common/Alert'
import { Button } from '../../components/common/Button'
import { useCurrentUser } from '../auth/api'
import { Nameplate } from './Nameplate'
import { useNameplate, useSelectNameplate } from './api'

export function NameplatePicker() {
  const { data: user } = useCurrentUser()
  const state = useNameplate()
  const select = useSelectNameplate()
  const [paused, setPaused] = useState(false)
  if (state.isPending) return <p role="status" className="animate-pulse text-muted">Effektlər yüklənir…</p>
  if (state.error) return <Alert title="Effektlər yüklənmədi">{state.error.message}</Alert>
  if (!user) return null
  const options = [{ id: null, name: 'Effektsiz', restricted: false, available: true }, ...state.data.options]
  return <section className={`space-y-4 ${paused ? 'nameplates-paused' : ''}`} aria-label="Profil effektləri">
    <div className="flex flex-wrap items-center justify-between gap-3">
      <div><h2 className="font-semibold">Profil effekti</h2><p className="text-sm text-muted">Seçim dərhal saxlanılır və menyudakı adınızın fonunda görünür.</p></div>
      <Button variant="secondary" onClick={() => setPaused(!paused)}>{paused ? <Play className="size-4" /> : <Pause className="size-4" />}{paused ? 'Önizləməni oynat' : 'Önizləməni dayandır'}</Button>
    </div>
    {state.data.isAdmin && <p className="text-sm text-muted">Admin olaraq bütün effektlər sizə açıqdır.</p>}
    {select.error && <Alert title="Effekt saxlanmadı">{select.error.message}</Alert>}
    {select.isSuccess && <p role="status" className="text-sm text-emerald-600">Profil effekti saxlanıldı.</p>}
    <div className="grid min-w-0 gap-4 xl:grid-cols-2">
      {options.map(option => <div key={option.id ?? 'none'} className="min-w-0 rounded-xl border border-line bg-surface p-3">
        <Nameplate effect={option.id} nickname={user.nickname} avatarUrl={user.avatarUrl} verified={user.emailConfirmed || user.googleLinked} />
        <div className="mt-3 flex flex-wrap items-center justify-between gap-2">
          <div><h3 className="text-sm font-semibold">{option.name}</h3><p className="text-xs text-muted">{option.restricted ? option.available ? 'Xüsusi · sizə açıqdır' : 'Admin icazəsi lazımdır' : 'Hamıya açıqdır'}</p></div>
          <Button variant={state.data.selected === option.id ? 'primary' : 'secondary'} disabled={!option.available || select.isPending || state.data.selected === option.id}
            aria-pressed={state.data.selected === option.id} onClick={() => select.mutate(option.id)}>
            {!option.available && <LockKeyhole className="size-4" />}{state.data.selected === option.id ? 'Aktivdir' : option.available ? 'Seç' : 'Kilidlidir'}
          </Button>
        </div>
      </div>)}
    </div>
  </section>
}
