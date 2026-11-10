import { GraduationCap, Layers, ShieldCheck, DoorOpen, FileUp, History, LayoutDashboard, Library, ListChecks, LogOut, Menu, School, Swords, Users, X, type LucideIcon } from 'lucide-react'
import { useEffect, useState } from 'react'
import { Link, NavLink, Outlet, useLocation, useSearchParams } from 'react-router'
import { useCurrentUser, useLogout } from '../features/auth/api'
import { tutorEnabled } from '../lib/features'
import { NotificationBell } from '../features/friends/NotificationBell'
import { cn } from '../lib/utils'
import { Nameplate } from '../features/nameplates/Nameplate'
import { useNameplate } from '../features/nameplates/api'
import { ThemeToggle } from '../components/common/ThemeToggle'

const navItems: { to: string; label: string; icon: LucideIcon; group: string; end?: boolean; adminOnly?: boolean; badge?: string }[] = [
  { to: '/', label: 'Dashboard', icon: LayoutDashboard, group: '', end: true },
  { to: '/upload', label: 'Sənəd yüklə', icon: FileUp, group: 'Hazırlıq' },
  { to: '/questions', label: 'Sual bankı', icon: Library, group: 'Hazırlıq' },
  { to: '/tutor', label: 'Öyrənmə bələdçisi', icon: Layers, group: 'Hazırlıq', badge: tutorEnabled ? 'Demo' : 'Tezliklə' },
  { to: '/exams/new', label: 'İmtahana başla', icon: ListChecks, group: 'İmtahan' },
  { to: '/exams', label: 'Tarixçə', icon: History, group: 'İmtahan', end: true },
  { to: '/dim/buraxilis', label: 'Buraxılış imtahanı', icon: School, group: 'DİM sınaqları' },
  { to: '/battle', label: 'Battle', icon: Swords, group: 'Birlikdə', end: true },
  { to: '/battle/rooms', label: 'Otaqlarım', icon: DoorOpen, group: 'Birlikdə' },
  { to: '/friends', label: 'Dostlar', icon: Users, group: 'Birlikdə' },
  { to: '/groups', label: 'Qruplarım', icon: GraduationCap, group: 'Birlikdə' },
  { to: '/admin', label: 'Admin Panel', icon: ShieldCheck, group: 'İdarə', adminOnly: true },
]

export function AppLayout() {
  const { data: user } = useCurrentUser()
  const [mobileOpen, setMobileOpen] = useState(false)
  // Any navigation (menu item, profile link, avatar) closes the mobile drawer; no link has to remember to do it.
  const { pathname } = useLocation()
  useEffect(() => { setMobileOpen(false) }, [pathname])
  const [isDesktop, setIsDesktop] = useState(() => window.matchMedia('(min-width: 1024px)').matches)
  useEffect(() => {
    const media = window.matchMedia('(min-width: 1024px)')
    const update = () => { setIsDesktop(media.matches); if (media.matches) setMobileOpen(false) }
    media.addEventListener('change', update)
    return () => media.removeEventListener('change', update)
  }, [])
  useEffect(() => {
    if (!mobileOpen) return
    const close = (event: KeyboardEvent) => { if (event.key === 'Escape') setMobileOpen(false) }
    const overflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    window.addEventListener('keydown', close)
    return () => { document.body.style.overflow = overflow; window.removeEventListener('keydown', close) }
  }, [mobileOpen])

  return (
    <div className="app-shell min-h-dvh lg:flex">
      {/* Mobile top bar */}
      <header className="sticky top-0 z-20 flex h-14 items-center justify-between border-b border-slate-200 bg-white px-4 lg:hidden">
        <Brand />
        <div className="ml-auto flex items-center gap-2">
          <ThemeToggle />
          <NotificationBell />
          <button
            type="button"
            onClick={() => setMobileOpen((open) => !open)}
            className="rounded-lg p-2 text-slate-600 hover:bg-slate-100"
            aria-label={mobileOpen ? 'Menyunu bağla' : 'Menyunu aç'}
            aria-expanded={mobileOpen}
          >
            {mobileOpen ? <X className="size-5" /> : <Menu className="size-5" />}
          </button>
        </div>
      </header>

      {/* Mobile: off-canvas drawer over a dimmed backdrop; desktop: fixed column beside the content. */}
      <div aria-hidden onClick={() => setMobileOpen(false)}
        className={cn('fixed inset-0 z-30 bg-slate-900/40 backdrop-blur-[2px] transition-opacity duration-300 lg:hidden', mobileOpen ? 'opacity-100' : 'pointer-events-none opacity-0')} />
      <aside
        inert={!mobileOpen && !isDesktop}
        className={cn(
          'app-sidebar fixed top-0 left-0 z-40 flex h-dvh w-72 max-w-[85vw] flex-col border-r border-slate-200 bg-white shadow-xl transition-transform duration-300 ease-out',
          'lg:sticky lg:top-0 lg:z-auto lg:h-dvh lg:w-64 lg:max-w-none lg:shrink-0 lg:translate-x-0 lg:shadow-none lg:transition-none',
          mobileOpen ? 'translate-x-0' : '-translate-x-full',
        )}
      >
        <div className="flex h-14 shrink-0 items-center justify-between px-4 lg:h-16 lg:px-6">
          <Brand />
          <button type="button" onClick={() => setMobileOpen(false)} className="rounded-lg p-2 text-slate-600 hover:bg-slate-100 lg:hidden" aria-label="Menyunu bağla"><X className="size-5" /></button>
        </div>
        <nav aria-label="Əsas naviqasiya" className="app-navigation flex min-h-0 flex-1 flex-col overflow-y-auto px-3 pb-3 pt-1">
          {[...new Set(navItems.map(item => item.group))].map(group => {
            const items = navItems.filter(item => item.group === group && (!item.adminOnly || user?.roles.includes('Admin')))
            if (!items.length) return null
            return <div key={group || 'home'} className="app-nav-group" role="group" aria-label={group || 'Ümumi'}>
              {group && <p className="ds-label" aria-hidden>{group}</p>}
              {items.map(({ to, label, icon: Icon, end, badge }) => (
                <NavLink key={to} to={to} end={end} onClick={() => setMobileOpen(false)}
                  className="app-nav-link flex h-9 items-center gap-2.5 rounded-lg px-2.5 text-[13.5px] font-medium transition-colors duration-150">
                  <Icon className="size-4 shrink-0" aria-hidden />
                  <span className="truncate">{label}</span>
                  {badge && <span className="ml-auto shrink-0 rounded-full bg-amber-100 px-1.5 py-px text-[10px] font-semibold tracking-wide text-amber-800 uppercase">{badge}</span>}
                </NavLink>
              ))}
            </div>
          })}
        </nav>
        <UserMenu />
      </aside>

      <div className="min-w-0 flex-1">
        <header className="app-toolbar hidden h-[72px] items-center justify-between border-b border-slate-200 px-8 lg:flex">
          <Breadcrumb />
          <div className="flex items-center gap-3"><ThemeToggle /><NotificationBell /></div>
        </header>
        <main className="app-content app-safe-bottom mx-auto max-w-[1200px] min-w-0 px-4 py-6 sm:px-6 lg:px-8 lg:py-8">
          <Outlet />
        </main>
      </div>
    </div>
  )
}

function UserMenu() {
  const { data: user } = useCurrentUser()
  const logout = useLogout()
  const nameplate = useNameplate()
  if (!user) return null
  const nickname = user.nickname || user.displayName || 'İstifadəçi'

  return (
    <div className="app-sidebar-footer shrink-0 border-t border-slate-200 p-2 lg:mt-auto">
      <Nameplate effect={nameplate.isError ? null : nameplate.data?.selected ?? null} nickname={nickname} avatarUrl={user.avatarUrl}
        verified={user.emailConfirmed || user.googleLinked}
        profileLink={<NavLink to="/profile" title={nickname} aria-label={`${nickname} — profili redaktə et`} className="nameplate-nickname">{nickname}</NavLink>}
        action={<button type="button" onClick={() => logout.mutate()} disabled={logout.isPending} aria-label="Çıxış" title="Çıxış"><LogOut className="size-4" /></button>} />
    </div>
  )
}

/** Logo = home: opens the dashboard, or scrolls it back to the top when already there. */
function Brand() {
  const { pathname } = useLocation()
  return (
    <Link to="/" aria-label="ExamR — Dashboard" title="Dashboard"
      onClick={event => {
        if (pathname !== '/') return
        event.preventDefault()
        window.scrollTo({ top: 0, behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth' })
      }}
      className="flex items-center gap-2.5 rounded-lg text-[17px] font-bold tracking-[-0.03em] transition-opacity duration-150 hover:opacity-80">
      <span className="app-brand-mark grid size-[30px] place-items-center rounded-lg" aria-hidden><svg viewBox="0 0 16 16" className="size-4"><path d="M4 4h8M4 8h5.5M4 12h8" stroke="currentColor" strokeWidth="2" strokeLinecap="round" /></svg></span>
      ExamR
    </Link>
  )
}

const extraPages: Record<string, string> = { '/notifications': 'Bildirişlər', '/profile': 'Profil' }
const adminSections: Record<string, string> = { documents: 'Sənədlər', groups: 'Qruplar', admins: 'Adminlər', dim: 'DİM sınaqları', nameplates: 'Profil effektləri' }

function Breadcrumb() {
  const { pathname } = useLocation()
  const [params] = useSearchParams()
  // Exact sidebar match first, otherwise the longest sidebar prefix (e.g. /groups/:id → Qruplarım).
  const item = navItems.find(i => i.to === pathname)
    ?? navItems.filter(i => i.to !== '/' && pathname.startsWith(`${i.to}/`)).sort((a, b) => b.to.length - a.to.length)[0]
  const crumbs: { label: string; to?: string }[] = []
  if (item) crumbs.push({ label: item.label, to: item.to })
  else if (extraPages[pathname]) crumbs.push({ label: extraPages[pathname] })
  if (pathname === '/admin') crumbs.push({ label: adminSections[params.get('section') ?? ''] ?? 'Sənədlər' })
  else if (pathname === '/admin/groups') crumbs.push({ label: 'Qruplar' })
  else if (/^\/groups\/[^/]+$/.test(pathname)) crumbs.push({ label: 'Qrup' })
  else if (/^\/exams\/[^/]+\/result$/.test(pathname)) crumbs.push({ label: 'Nəticə' })
  return <nav aria-label="Breadcrumb" className="flex min-w-0 items-center gap-2 text-xs">
    <Link to="/" className="font-semibold text-slate-700 transition-colors hover:text-indigo-600">ExamR</Link>
    {crumbs.map((c, i) => <span key={c.label} className="flex min-w-0 items-center gap-2">
      <span className="text-slate-400" aria-hidden>/</span>
      {c.to && i < crumbs.length - 1
        ? <Link to={c.to} className="truncate text-slate-500 transition-colors hover:text-indigo-600">{c.label}</Link>
        : <span aria-current={i === crumbs.length - 1 ? 'page' : undefined} className={cn('truncate', i === crumbs.length - 1 ? 'font-medium text-slate-700' : 'text-slate-500')}>{c.label}</span>}
    </span>)}
  </nav>
}
