import { NameplatePicker } from '../nameplates/NameplatePicker'
import { VerifiedBadge } from '../../components/common/VerifiedBadge'
import './profile.css'
import { AccountSettings } from './AccountSettings'
import { ExamThemeSettings } from '../exams/ExamThemeSettings'
import { RankBadge } from '../battle/RankSummary'
import { useBattleRank } from '../battle/rank-api'
import { Check, ClipboardList, ImagePlus, LayoutGrid, Palette, Pencil, Save, Settings, ShieldCheck, Swords, Trash2, UserRound, Users } from 'lucide-react'
import { useEffect, useRef, useState, type FormEvent } from 'react'
import { useBlocker, useSearchParams } from 'react-router'
import { useQueryClient } from '@tanstack/react-query'
import { Alert } from '../../components/common/Alert'
import { Button } from '../../components/common/Button'
import { PageHeader } from '../../components/common/PageHeader'
import { api, uploadFile } from '../../lib/api-client'
import { queryKeys } from '../../lib/query-keys'
import { useConfirm } from '../../lib/use-confirm'
import { cn } from '../../lib/utils'
import { normalizeUser, useCurrentUser, type User } from './api'
import { ProfileActivity } from './ProfileActivity'
import { useTranslation } from 'react-i18next'
import i18n from '../../lib/i18n'
import { squareAvatar } from '../../lib/avatar-image'

// Phones show the short label under the icon so all five sections fit without scrolling.
const tabs = [
  { id: 'overview', icon: LayoutGrid },
  { id: 'exams', icon: ClipboardList },
  { id: 'battle', icon: Swords },
  { id: 'social', icon: Users },
  { id: 'settings', icon: Settings },
]
const field = 'w-full rounded-xl border border-slate-300 bg-white px-3 py-2.5 text-sm focus:border-indigo-500 focus:outline-none focus:ring-2 focus:ring-indigo-500/20'

export function ProfilePage() {
  const rank = useBattleRank()
  const client = useQueryClient()
  const current = useCurrentUser()
  const user = current.data
  const [params, setParams] = useSearchParams()
  const requested = Number(params.get('tab') ?? (params.has('google') ? 4 : 0))
  const tab = Number.isInteger(requested) && requested >= 0 && requested < tabs.length ? requested : 0
  const [editing, setEditing] = useState(false)
  const [nickname, setNickname] = useState('')
  const [bio, setBio] = useState('')
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [saving, setSaving] = useState(false)
  const [avatarBusy, setAvatarBusy] = useState(false)
  const [brokenAvatar, setBrokenAvatar] = useState<string | null>(null)
  const input = useRef<HTMLInputElement>(null)
  const confirm = useConfirm()
  const { t } = useTranslation()
  const dirty = editing && (nickname !== user?.nickname || bio !== (user?.bio ?? ''))
  const blocker = useBlocker(({ currentLocation, nextLocation }) => dirty && currentLocation.pathname !== nextLocation.pathname)
  useEffect(() => {
    if (!dirty) return
    const warn = (event: BeforeUnloadEvent) => { event.preventDefault(); event.returnValue = '' }
    window.addEventListener('beforeunload', warn)
    return () => window.removeEventListener('beforeunload', warn)
  }, [dirty])
  useEffect(() => {
    if (blocker.state !== 'blocked') return
    let active = true
    void confirm({ title: t('profile.unsavedTitle'), description: t('profile.unsavedText'), confirmLabel: t('profile.leave') }).then(leave => {
      if (active) { if (leave) blocker.proceed(); else blocker.reset() }
    })
    return () => { active = false }
  }, [blocker, confirm, t])

  if (current.isPending) return <p role="status" className="animate-pulse text-slate-500">{t('profile.loading')}</p>
  if (current.isError) return <Alert title={t('profile.loadFailed')}>{current.error.message}</Alert>
  if (!user) return null

  function edit() { setNickname(user!.nickname); setBio(user!.bio ?? ''); setError(''); setNotice(''); setEditing(true) }
  async function cancel() {
    if (dirty && !await confirm({ title: t('profile.discardTitle'), description: t('profile.discardText'), confirmLabel: t('profile.discard') })) return
    setEditing(false); setError('')
  }
  async function save(event: FormEvent) {
    event.preventDefault(); setSaving(true); setError(''); setNotice('')
    try {
      const fresh = normalizeUser(await api.put<User>('/auth/profile', { nickname, bio }))
      client.setQueryData(queryKeys.me, fresh)
      setEditing(false); setNotice(t('profile.updated'))
    } catch (cause) { setError(cause instanceof Error ? cause.message : t('profile.saveFailed')) }
    finally { setSaving(false) }
  }
  async function avatar(file?: File, remove = false) {
    if (!remove && !file) return
    setError(''); setNotice('')
    if (file && (file.size > 5 * 1024 * 1024 || !['image/png', 'image/jpeg', 'image/webp'].includes(file.type))) {
      setError(t('profile.avatarRules')); return
    }
    setAvatarBusy(true)
    try {
      if (remove) await api.delete('/profile/avatar'); else await uploadFile('/profile/avatar', await squareAvatar(file!))
      const fresh = normalizeUser(await api.get<User>('/auth/me'))
      client.setQueryData(queryKeys.me, fresh)
      setNotice(t('profile.avatarSaved'))
    } catch (cause) { setError(cause instanceof Error ? cause.message : t('profile.avatarFailed')) }
    finally { setAvatarBusy(false); if (input.current) input.current.value = '' }
  }
  return <div className="mx-auto min-w-0 max-w-5xl space-y-5">
    <PageHeader title={t('profile.title')} description={t('profile.description')} />
    {error && <Alert title={t('profile.actionFailed')}>{error}</Alert>}
    {notice && <div role="status"><Alert tone="info" title={notice} /></div>}
    <section aria-label={t('profile.infoAria')} className="rounded-xl border border-slate-200 bg-white p-4 sm:p-6">
      <div className="flex flex-wrap items-start gap-x-4 gap-y-3 sm:gap-5">
        {user.avatarUrl && brokenAvatar !== user.avatarUrl
          ? <img src={user.avatarUrl} onError={() => setBrokenAvatar(user.avatarUrl)} alt={t('profile.avatarAlt', { name: user.nickname })} className="size-14 shrink-0 rounded-full border border-slate-200 object-cover sm:size-20" />
          : <span className="grid size-14 shrink-0 place-items-center rounded-full bg-indigo-50 text-indigo-600 sm:size-20"><UserRound className="size-7 sm:size-8" aria-hidden /></span>}
        <div className="min-w-0 flex-1 self-center">
          <p className="text-xs font-medium uppercase tracking-widest text-slate-500">{t('profile.personal')}</p>
          <h2 className="mt-0.5 text-xl font-semibold tracking-tight [overflow-wrap:anywhere] sm:mt-1 sm:text-2xl">{user.nickname}{(user.emailConfirmed || user.googleLinked) && <VerifiedBadge className="ml-1.5" />}</h2>
          {rank.data && <div className="mt-2"><RankBadge rank={rank.data} /></div>}
          <p className="mt-2 whitespace-pre-wrap text-sm text-slate-500">{user.bio || t('profile.bioPlaceholder')}</p>
        </div>
        {!editing && <Button variant="secondary" onClick={edit} className="w-full sm:w-auto"><Pencil className="size-4" aria-hidden />{t('profile.edit')}</Button>}
      </div>
      {editing && <form onSubmit={save} className="mt-6 space-y-4 border-t border-slate-200 pt-5">
        <fieldset disabled={saving || avatarBusy} className="space-y-4 disabled:opacity-70">
          <legend className="sr-only">{t('profile.edit')}</legend>
          <div className="flex flex-wrap gap-2">
            <input ref={input} type="file" accept="image/png,image/jpeg,image/webp" className="sr-only" aria-label={t('profile.pickAvatar')} onChange={e => void avatar(e.target.files?.[0])} />
            <Button variant="secondary" loading={avatarBusy} onClick={() => input.current?.click()}><ImagePlus className="size-4" aria-hidden />{t('profile.changeAvatar')}</Button>
            {user.avatarUrl && <Button variant="ghost" onClick={() => void avatar(undefined, true)}><Trash2 className="size-4" aria-hidden />{t('profile.removeAvatar')}</Button>}
          </div>
          <p className="text-xs text-slate-500">{t('profile.avatarHint')}</p>
          <label className="block"><span className="mb-1.5 block text-sm font-medium">Nickname</span>
            <input className={field} value={nickname} onChange={e => setNickname(e.target.value)} minLength={3} maxLength={24} pattern="[A-Za-z0-9_]{3,24}" required aria-describedby="nickname-hint" />
            <span id="nickname-hint" className="mt-1 block text-xs text-slate-500">{t('profile.nicknameHint')}</span>
          </label>
          <label className="block"><span className="mb-1.5 block text-sm font-medium">Bio</span>
            <textarea className={cn(field, 'resize-y')} value={bio} onChange={e => setBio(e.target.value)} maxLength={160} rows={3} aria-describedby="bio-counter" />
            <span id="bio-counter" className="mt-1 block text-right text-xs tabular-nums text-slate-500">{bio.length}/160</span>
          </label>
        </fieldset>
        <div className="flex flex-wrap gap-2"><Button type="submit" disabled={!dirty || avatarBusy} loading={saving}><Save className="size-4" aria-hidden />{t('common.save')}</Button><Button variant="secondary" disabled={saving || avatarBusy} onClick={() => void cancel()}>{t('common.cancel')}</Button></div>
      </form>}
    </section>
    <div role="tablist" aria-label={t('profile.tabsAria')} className="profile-tabs">
      {tabs.map(({ id, icon: Icon }, index) => <button key={id} role="tab" id={`profile-tab-${index}`} aria-selected={tab === index} aria-controls="profile-panel" tabIndex={tab === index ? 0 : -1}
        onClick={() => setParams(previous => { const next = new URLSearchParams(previous); next.set('tab', String(index)); return next }, { replace: true })}
        onKeyDown={event => {
          const next = event.key === 'ArrowRight' ? (index + 1) % tabs.length : event.key === 'ArrowLeft' ? (index + tabs.length - 1) % tabs.length : event.key === 'Home' ? 0 : event.key === 'End' ? tabs.length - 1 : null
          if (next !== null) { event.preventDefault(); document.getElementById(`profile-tab-${next}`)?.click(); document.getElementById(`profile-tab-${next}`)?.focus() }
        }}
        className="profile-tab"><Icon className="size-[18px] shrink-0 md:size-4" aria-hidden /><span className="md:hidden">{t(`profile.tabs.${id}.short`)}</span><span className="hidden md:inline">{t(`profile.tabs.${id}.label`)}</span></button>)}
    </div>
    <div key={tab} className="profile-panel" role="tabpanel" id="profile-panel" aria-labelledby={`profile-tab-${tab}`} tabIndex={0}>
      {tab === 4 ? <SettingsPanel /> : <ProfileActivity tab={tab} />}
    </div>
  </div>
}
const linkCodes = ['linked', 'google_in_use', 'google_disabled', 'google_cancelled', 'login_required']

const settingsSections = [
  { id: 'account', icon: ShieldCheck },
  { id: 'custom', icon: Palette },
] as const

// Account settings come first; cosmetic options (effect, exam theme) live in their own sub-section.
function SettingsPanel() {
  const [params, setParams] = useSearchParams()
  const section = params.get('section') === 'custom' ? 'custom' : 'account'
  const { t } = useTranslation()
  return <div className="space-y-4">
    <div role="group" aria-label={t('profile.settingsAria')} className="grid grid-cols-2 gap-1 rounded-xl border border-line bg-surface p-1 sm:inline-grid sm:w-auto">
      {settingsSections.map(({ id, icon: Icon }) => <button key={id} type="button" aria-pressed={section === id}
        onClick={() => setParams(previous => { const next = new URLSearchParams(previous); next.set('section', id); return next }, { replace: true })}
        className={cn('flex min-h-10 items-center justify-center gap-2 rounded-lg px-4 text-sm font-medium transition-colors focus-visible:outline-2 focus-visible:outline-accent',
          section === id ? 'bg-accent-soft font-semibold text-accent' : 'text-muted hover:bg-sunken')}>
        <Icon className="size-4 shrink-0" aria-hidden />{t(`profile.settings.${id}`)}
      </button>)}
    </div>
    {section === 'account'
      ? <><AccountSettings /><GoogleLinkSection /></>
      : <><NameplatePicker /><ExamThemeSettings /></>}
  </div>
}

function GoogleLinkSection() {
  const { data: user } = useCurrentUser()
  const linked = !!user?.googleLinked
  const { t } = useTranslation()
  const [result] = useState(() => {
    const code = new URLSearchParams(window.location.search).get('google')
    if (code) window.history.replaceState(null, '', window.location.pathname)
    return code ? { ok: code === 'linked', text: i18n.t(linkCodes.includes(code) ? `profile.google.${code}` : 'profile.google.failed') } : null
  })
  return <section className="rounded-xl border border-slate-200 bg-white p-5">
    <h2 className="font-semibold">{t('profile.google.title')}</h2>
    {/* The success message is only needed right after linking; afterwards the status line says it. */}
    {result && !(result.ok && linked) && <div className="mt-4"><Alert tone={result.ok ? 'info' : 'danger'} title={result.text} /></div>}
    {linked
      ? <p className="mt-3 flex items-center gap-2 text-sm"><span className="grid size-5 place-items-center rounded-full bg-emerald-600 text-white" aria-hidden><Check className="size-3" strokeWidth={3} /></span>
          <span><b className="font-semibold">{t('profile.google.linkedTitle')}</b> <span className="text-slate-500">{t('profile.google.linkedText')}</span></span></p>
      : <>
          <p className="mt-1 text-sm text-slate-500">{t('profile.google.linkText')}</p>
          <a href="/api/auth/google?link=true" className="mt-4 inline-flex h-10 items-center rounded-xl border border-slate-300 px-4 text-sm font-medium transition-colors hover:bg-slate-50">{t('profile.google.link')}</a>
        </>}
  </section>
}
