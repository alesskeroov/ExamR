import { LockKeyhole, Pause, Play, Sparkles, X } from 'lucide-react'
import { useEffect, useId, useRef, useState } from 'react'
import { Alert } from '../../components/common/Alert'
import { Button } from '../../components/common/Button'
import { useCurrentUser, type User } from '../auth/api'
import { Nameplate } from './Nameplate'
import { useNameplate, useSelectNameplate } from './api'

/** Settings card: shows only the active effect; the full (animated) gallery opens in a dialog on demand. */
export function NameplatePicker() {
  const { data: user } = useCurrentUser()
  const state = useNameplate()
  const [open, setOpen] = useState(false)
  if (state.isPending) return <p role="status" className="animate-pulse text-muted">Effektlər yüklənir…</p>
  if (state.error) return <Alert title="Effektlər yüklənmədi">{state.error.message}</Alert>
  if (!user) return null
  const active = state.data.options.find(option => option.id === state.data.selected)
  return <section aria-labelledby="nameplate-heading" className="rounded-xl border border-line bg-surface p-4 sm:p-5">
    <div className="flex flex-wrap items-start justify-between gap-3">
      <div className="min-w-0">
        <h2 id="nameplate-heading" className="font-semibold">Profil effekti</h2>
        <p className="mt-1 text-sm text-muted">Menyudakı adınızın fonunda görünür. Hazırda: <b className="font-semibold text-ink">{active?.name ?? 'Effektsiz'}</b></p>
      </div>
      <Button variant="secondary" className="w-full sm:w-auto" onClick={() => setOpen(true)}><Sparkles className="size-4" aria-hidden />Effekti dəyiş</Button>
    </div>
    <div className="mt-4 min-w-0 max-w-md">
      <Nameplate effect={state.data.selected} nickname={user.nickname} avatarUrl={user.avatarUrl} verified={user.emailConfirmed || user.googleLinked} />
    </div>
    {open && <NameplateDialog user={user} onClose={() => setOpen(false)} />}
  </section>
}

function NameplateDialog({ user, onClose }: { user: User; onClose: () => void }) {
  const ref = useRef<HTMLDialogElement>(null)
  const titleId = useId()
  const state = useNameplate()
  const select = useSelectNameplate()
  const [paused, setPaused] = useState(false)
  useEffect(() => { const dialog = ref.current; dialog?.showModal(); return () => dialog?.close() }, [])
  if (!state.data) return null
  const options = [{ id: null, name: 'Effektsiz', restricted: false, available: true }, ...state.data.options]
  return <dialog ref={ref} aria-labelledby={titleId} onCancel={event => { event.preventDefault(); onClose() }}
    onClick={event => { if (event.target === ref.current) onClose() }}
    className="m-auto max-h-[90dvh] open:flex w-[calc(100%-1.5rem)] max-w-4xl flex-col overflow-hidden rounded-xl border border-line bg-surface p-0 text-ink shadow-xl backdrop:bg-slate-900/50 sm:w-[calc(100%-2rem)]">
    <div className="flex flex-wrap items-center justify-between gap-2 border-b border-line px-4 py-3 sm:px-5">
      <h2 id={titleId} className="text-lg font-semibold">Profil effekti seçin</h2>
      <div className="flex items-center gap-1">
        <Button variant="ghost" className="h-9 px-2.5 text-sm" onClick={() => setPaused(!paused)}>
          {paused ? <Play className="size-4" aria-hidden /> : <Pause className="size-4" aria-hidden />}<span className="hidden sm:inline">{paused ? 'Önizləməni oynat' : 'Önizləməni dayandır'}</span>
          <span className="sr-only sm:hidden">{paused ? 'Önizləməni oynat' : 'Önizləməni dayandır'}</span>
        </Button>
        <Button variant="ghost" className="size-9 p-0" aria-label="Bağla" onClick={onClose}><X className="size-4" aria-hidden /></Button>
      </div>
    </div>
    <div className={`min-h-0 flex-1 space-y-3 overflow-y-auto p-3 sm:p-5 ${paused ? 'nameplates-paused' : ''}`}>
      {state.data.isAdmin && <p className="text-sm text-muted">Admin olaraq bütün effektlər sizə açıqdır.</p>}
      {select.error && <Alert title="Effekt saxlanmadı">{select.error.message}</Alert>}
      <div className="grid min-w-0 gap-3 md:grid-cols-2">
        {options.map(option => {
          const selected = state.data.selected === option.id
          return <div key={option.id ?? 'none'} className={`min-w-0 rounded-xl border bg-surface p-3 ${selected ? 'border-accent ring-1 ring-accent' : 'border-line'}`}>
            <Nameplate effect={option.id} nickname={user.nickname} avatarUrl={user.avatarUrl} verified={user.emailConfirmed || user.googleLinked} />
            <div className="mt-3 flex flex-wrap items-center justify-between gap-2">
              <div className="min-w-0"><h3 className="text-sm font-semibold">{option.name}</h3><p className="text-xs text-muted">{option.restricted ? option.available ? 'Xüsusi · sizə açıqdır' : 'Admin icazəsi lazımdır' : 'Hamıya açıqdır'}</p></div>
              <Button variant={selected ? 'primary' : 'secondary'} disabled={!option.available || select.isPending || selected}
                aria-pressed={selected} onClick={() => select.mutate(option.id, { onSuccess: onClose })}>
                {!option.available && <LockKeyhole className="size-4" aria-hidden />}{selected ? 'Aktivdir' : option.available ? 'Seç' : 'Kilidlidir'}
              </Button>
            </div>
          </div>
        })}
      </div>
    </div>
  </dialog>
}
