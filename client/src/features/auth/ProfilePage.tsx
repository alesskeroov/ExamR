import { Check, ImagePlus, Save, Trash2, UserRound } from 'lucide-react'
import { useRef, useState, type FormEvent } from 'react'
import { Alert } from '../../components/common/Alert'
import { Button } from '../../components/common/Button'
import { PageHeader } from '../../components/common/PageHeader'
import { api, uploadFile } from '../../lib/api-client'
import { queryKeys } from '../../lib/query-keys'
import { useQueryClient } from '@tanstack/react-query'
import { normalizeUser, useCurrentUser, type User } from './api'

export function ProfilePage() {
  const client = useQueryClient()
  const { data: user } = useCurrentUser()
  const [nickname, setNickname] = useState(user?.nickname ?? '')
  const [preview, setPreview] = useState<string | null>(null)
  const [error, setError] = useState('')
  const [saving, setSaving] = useState(false)
  const input = useRef<HTMLInputElement>(null)
  if (!user) return null

  async function refresh() {
    const fresh = normalizeUser(await api.get<User>('/auth/me'))
    client.setQueryData(queryKeys.me, fresh)
    setNickname(fresh.nickname)
    setPreview(null)
  }

  async function save(e: FormEvent) {
    e.preventDefault(); setError(''); setSaving(true)
    try {
      const fresh = await api.put<User>('/auth/profile', { nickname })
      client.setQueryData(queryKeys.me, fresh)
    } catch (cause) { setError(cause instanceof Error ? cause.message : 'Profil saxlanmadı.') }
    finally { setSaving(false) }
  }

  async function upload(file?: File) {
    if (!file) return
    setError('')
    setPreview(URL.createObjectURL(file))
    try { await uploadFile('/profile/avatar', file); await refresh() }
    catch (cause) { setError(cause instanceof Error ? cause.message : 'Şəkil yüklənmədi.'); setPreview(null) }
  }

  async function removeAvatar() {
    setError('')
    try { await api.delete('/profile/avatar'); await refresh() }
    catch (cause) { setError(cause instanceof Error ? cause.message : 'Şəkil silinmədi.') }
  }

  return <>
    <PageHeader title="Profil" description="Nickname və profil şəklinizi idarə edin." />
    {error && <div className="mb-4"><Alert title="Əməliyyat alınmadı">{error}</Alert></div>}
    <section className="max-w-2xl rounded-xl border border-slate-200 bg-white p-5 sm:p-7">
      <div className="mb-7 flex flex-wrap items-center gap-4">
        {preview || user.avatarUrl ? <img src={preview ?? user.avatarUrl ?? ''} alt="Profil şəkli" className="size-20 rounded-full border border-slate-200 object-cover" /> :
          <span className="flex size-20 items-center justify-center rounded-full bg-slate-100 text-slate-500"><UserRound className="size-8" aria-hidden /></span>}
        <div className="flex flex-wrap gap-2">
          <input ref={input} type="file" accept="image/png,image/jpeg,image/webp" className="sr-only" aria-label="Profil şəkli seç" onChange={e => void upload(e.target.files?.[0])} />
          <Button variant="secondary" onClick={() => input.current?.click()}><ImagePlus className="size-4" aria-hidden />Şəkil yüklə</Button>
          {user.avatarUrl && <Button variant="ghost" onClick={() => void removeAvatar()}><Trash2 className="size-4" aria-hidden />Sil</Button>}
        </div>
      </div>
      <form onSubmit={save} className="space-y-5">
        <label className="block"><span className="mb-1.5 block text-sm font-medium">Nickname</span>
          <input value={nickname} onChange={e => setNickname(e.target.value)} minLength={3} maxLength={24} pattern="[A-Za-z0-9_]{3,24}" required
            className="h-11 w-full rounded-lg border border-slate-300 px-3 focus:border-indigo-500 focus:ring-2 focus:ring-indigo-500/20 focus:outline-none" />
          <span className="mt-1 block text-xs text-slate-500">3–24 simvol: ingilis hərfləri, rəqəmlər və alt xətt.</span>
        </label>
        <label className="block"><span className="mb-1.5 block text-sm font-medium">Hesab emaili</span>
          <input value={user.email} readOnly className="h-11 w-full rounded-lg border border-slate-200 bg-slate-50 px-3 text-slate-500" />
        </label>
        <Button type="submit" disabled={saving || nickname === user.nickname}><Save className="size-4" aria-hidden />{saving ? 'Saxlanır…' : 'Dəyişiklikləri saxla'}</Button>
      </form>
    </section>
    <GoogleLinkSection />
  </>
}

const linkMessages: Record<string, { ok: boolean; text: string }> = {
  linked: { ok: true, text: 'Google hesabı əlaqələndirildi. İndi Google ilə də daxil ola bilərsiniz.' },
  google_in_use: { ok: false, text: 'Bu Google hesabı başqa ExamR hesabına bağlıdır.' },
  google_disabled: { ok: false, text: 'Google ilə giriş hələ konfiqurasiya edilməyib.' },
  google_cancelled: { ok: false, text: 'Google-da icazə verilmədi və ya əməliyyat ləğv edildi.' },
  login_required: { ok: false, text: 'Əlaqələndirmək üçün yenidən daxil olun.' },
}

function GoogleLinkSection() {
  const { data: user } = useCurrentUser()
  const linked = !!user?.googleLinked
  const [result] = useState(() => {
    const code = new URLSearchParams(window.location.search).get('google')
    if (code) window.history.replaceState(null, '', window.location.pathname)
    return code ? linkMessages[code] ?? { ok: false, text: 'Google hesabı əlaqələndirilmədi.' } : null
  })
  return <section className="mt-6 max-w-2xl rounded-xl border border-slate-200 bg-white p-5 sm:p-7">
    <h2 className="font-semibold">Google hesabı</h2>
    {/* The success message is only needed right after linking; afterwards the status line says it. */}
    {result && !(result.ok && linked) && <div className="mt-4"><Alert tone={result.ok ? 'info' : 'danger'} title={result.text} /></div>}
    {linked
      ? <p className="mt-3 flex items-center gap-2 text-sm"><span className="grid size-5 place-items-center rounded-full bg-emerald-600 text-white" aria-hidden><Check className="size-3" strokeWidth={3} /></span>
          <span><b className="font-semibold">Əlaqələndirilib.</b> <span className="text-slate-500">Bu hesaba “Google ilə davam et” düyməsi ilə də daxil ola bilərsiniz.</span></span></p>
      : <>
          <p className="mt-1 text-sm text-slate-500">Hesabınızı Google ilə əlaqələndirin ki, növbəti dəfə bir kliklə daxil olasınız.</p>
          <a href="/api/auth/google?link=true" className="mt-4 inline-flex h-10 items-center rounded-xl border border-slate-300 px-4 text-sm font-medium transition-colors hover:bg-slate-50">Google hesabını əlaqələndir</a>
        </>}
  </section>
}
