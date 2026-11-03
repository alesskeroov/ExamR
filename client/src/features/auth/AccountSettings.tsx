import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Check, MailCheck, MailWarning } from 'lucide-react'
import { useEffect, useState, type FormEvent } from 'react'
import { useNavigate, useSearchParams } from 'react-router'
import { Alert } from '../../components/common/Alert'
import { Button } from '../../components/common/Button'
import { api } from '../../lib/api-client'
import { queryKeys } from '../../lib/query-keys'
import { useConfirm } from '../../lib/use-confirm'
import { useCurrentUser, useForgotPassword, useResendConfirmation } from './api'

const card = 'rounded-xl border border-slate-200 bg-white p-5'
const field = 'mt-1.5 w-full rounded-xl border border-slate-300 bg-white px-3 py-2.5 focus:border-indigo-500 focus:outline-none focus:ring-2 focus:ring-indigo-500/20'

export function AccountSettings() {
  const { data: user } = useCurrentUser()
  const resend = useResendConfirmation()
  const reset = useForgotPassword()
  const client = useQueryClient()
  const navigate = useNavigate()
  const [params] = useSearchParams()
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [confirmation, setConfirmation] = useState('')
  const [cooldownUntil, setCooldownUntil] = useState(0)
  const cooldown = cooldownUntil > Date.now()
  useEffect(() => {
    if (!cooldownUntil) return
    const timer = window.setTimeout(() => setCooldownUntil(0), Math.max(0, cooldownUntil - Date.now()))
    return () => window.clearTimeout(timer)
  }, [cooldownUntil])
  const [notice, setNotice] = useState('')
  const confirm = useConfirm()
  const signedOut = (reason: string) => { client.clear(); client.setQueryData(queryKeys.me, null); navigate(`/?auth=login&account=${reason}`, { replace: true }) }
  const password = useMutation({ mutationFn: () => api.post('/account/password', { currentPassword, newPassword, confirmPassword }), onSuccess: () => signedOut('password-changed') })
  const deletionLink = useMutation({ mutationFn: () => api.post<{ message: string }>('/account/deletion-link') })
  const deletion = useMutation({ mutationFn: () => api.delete('/account', { body: { token: params.get('deleteToken'), confirmation } }), onSuccess: () => signedOut('deleted') })
  const busy = password.isPending || deletion.isPending || deletionLink.isPending
  if (!user) return null
  async function sendVerification() {
    if (Date.now() < cooldownUntil) return
    setCooldownUntil(Date.now() + 60_000)
    resend.mutate(user!.email)
  }
  async function change(event: FormEvent) { event.preventDefault(); if (newPassword !== confirmPassword) { setNotice('Yeni şifrələr uyğun gəlmir.'); return }; setNotice(''); password.mutate() }
  async function remove() {
    if (await confirm({ title: 'Hesab bağlansın?', description: 'Profil anonimləşdiriləcək, giriş və aktiv sessiyalar bağlanacaq. Ortaq sənədlər, qruplar və tarixçə qorunacaq. Bu əməliyyatı geri qaytarmaq mümkün deyil.', confirmLabel: 'Hesabı bağla', danger: true })) deletion.mutate()
  }
  return <div className="space-y-4">
    <section className={card}>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 className="font-semibold">E-mail təsdiqi</h2>
        {user.emailConfirmed
          ? <span className="inline-flex items-center gap-1.5 rounded-full bg-emerald-50 px-2.5 py-1 text-xs font-semibold text-emerald-700 ring-1 ring-emerald-600/20"><MailCheck className="size-3.5" aria-hidden />Təsdiqlənib</span>
          : <span className="inline-flex items-center gap-1.5 rounded-full bg-amber-50 px-2.5 py-1 text-xs font-semibold text-amber-800 ring-1 ring-amber-600/25"><MailWarning className="size-3.5" aria-hidden />Təsdiqlənməyib</span>}
      </div>
      {user.emailConfirmed ? (
        <div className="mt-4 flex items-center gap-3 rounded-xl bg-emerald-50/60 px-3.5 py-3 ring-1 ring-emerald-600/15">
          <span className="grid size-9 shrink-0 place-items-center rounded-full bg-emerald-600 text-white" aria-hidden><Check className="size-4" strokeWidth={3} /></span>
          <div className="min-w-0">
            <p className="break-all text-sm font-semibold text-slate-900">{user.email}</p>
            <p className="text-xs text-emerald-700">Email ünvanınız təsdiqlənib. Bildiriş və bərpa məktubları bu ünvana göndərilir.</p>
          </div>
        </div>
      ) : (
        <>
          <div className="mt-4 rounded-xl bg-amber-50/50 px-3.5 py-3 ring-1 ring-amber-600/20">
            <p className="break-all text-sm font-semibold text-slate-900">{user.email}</p>
            <p className="mt-0.5 text-xs text-amber-800">Hesabı qorumaq və şifrəni bərpa edə bilmək üçün emailinizi təsdiqləyin.</p>
          </div>
          <div className="mt-4 flex flex-col gap-2 sm:flex-row sm:flex-wrap">
            <Button variant="secondary" loading={resend.isPending} disabled={cooldown} onClick={() => void sendVerification()} className="w-full sm:w-auto">{cooldown ? 'Yenidən göndərmək üçün 60 saniyə gözləyin' : resend.isSuccess ? 'Yenidən göndər' : 'Təsdiqləmə məktubu göndər'}</Button>
            <Button variant="ghost" onClick={() => void client.invalidateQueries({ queryKey: queryKeys.me })} className="w-full sm:w-auto">Təsdiq statusunu yenilə</Button>
          </div>
        </>
      )}
      {resend.isSuccess && <div role="status" className="mt-3"><Alert tone="info" title={resend.data.message} /></div>}{resend.isError && <Alert title="Məktub göndərilmədi">{resend.error.message}</Alert>}
    </section>
    <section className={card}><h2 className="font-semibold">Təhlükəsizlik</h2>
      {user.hasPassword !== false ? <form onSubmit={change} className="mt-4 space-y-3 text-sm">
        <label className="block">Cari şifrə<input className={field} type="password" autoComplete="current-password" value={currentPassword} onChange={e => setCurrentPassword(e.target.value)} required maxLength={128} /></label>
        <label className="block">Yeni şifrə<input className={field} type="password" autoComplete="new-password" value={newPassword} onChange={e => setNewPassword(e.target.value)} required minLength={8} maxLength={128} /></label>
        <label className="block">Yeni şifrənin təkrarı<input className={field} type="password" autoComplete="new-password" value={confirmPassword} onChange={e => setConfirmPassword(e.target.value)} required minLength={8} maxLength={128} /></label>
        <p className="text-xs text-slate-500">Ən az 8 simvol, böyük və kiçik hərf, rəqəm. Dəyişiklikdən sonra bütün sessiyalar bağlanacaq.</p>
        <Button type="submit" loading={password.isPending} disabled={busy || !currentPassword || !newPassword || !confirmPassword}>Şifrəni dəyiş</Button>
      </form> : <p className="mt-3 text-sm text-slate-500">Hesabınıza Google ilə daxil olursunuz. Lokal şifrə təyin etmək üçün email ünvanınıza 30 dəqiqə etibarlı bərpa linki göndərin; cari şifrə tələb olunmur.</p>}
      <Button className="mt-4" variant="secondary" loading={reset.isPending} onClick={() => reset.mutate(user.email)}>{user.hasPassword === false ? 'Şifrə təyin etmə linki göndər' : 'Şifrə bərpa linki göndər'}</Button>
      {reset.isSuccess && <div role="status" className="mt-3"><Alert tone="info" title={reset.data.message} /></div>}{reset.isError && <Alert title="Link göndərilmədi">{reset.error.message}</Alert>}
      {notice && <Alert title={notice} />}{password.isError && <Alert title="Şifrə dəyişdirilmədi">{password.error.message}</Alert>}
    </section>
    <section className="rounded-xl border border-rose-200 bg-white p-5"><h2 className="font-semibold text-rose-700">Təhlükəli əməliyyatlar</h2><p className="mt-3 text-sm text-slate-500">Hesabınız bağlanır və şəxsi profil anonimləşdirilir. Ortaq sənədlər və nəticə tarixçələri silinmir. Təsdiq emailinə çıxış tələb olunur; son administrator hesabı bağlana bilməz.</p>
      {params.get('deleteToken') && params.get('uid') === user.id ? <div className="mt-4 space-y-3"><label className="block text-sm">Təsdiq üçün SİL yazın<input className={field} autoComplete="off" value={confirmation} onChange={e => setConfirmation(e.target.value)} maxLength={3} /></label><Button variant="secondary" disabled={confirmation !== 'SİL' || busy} loading={deletion.isPending} onClick={() => void remove()}>Hesabı sil</Button></div>
        : <Button className="mt-4" variant="secondary" loading={deletionLink.isPending} disabled={busy} onClick={() => deletionLink.mutate()}>Hesabı sil — təsdiq linki göndər</Button>}
      {deletionLink.isSuccess && <div role="status" className="mt-3"><Alert tone="info" title={deletionLink.data.message} /></div>}
      {(deletionLink.error || deletion.error) && <Alert title="Hesab bağlanmadı">{(deletionLink.error || deletion.error)?.message}</Alert>}
    </section>
  </div>
}
