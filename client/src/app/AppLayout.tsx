import { GraduationCap, ShieldCheck, Sparkles, DoorOpen, FileUp, History, LayoutDashboard, Library, ListChecks, LogOut, Menu, Swords, Users, X, type LucideIcon } from 'lucide-react'
import { useState } from 'react'
import { NavLink, Outlet } from 'react-router'
import { useCurrentUser, useLogout } from '../features/auth/api'
import { NotificationBell } from '../features/friends/NotificationBell'
import { cn } from '../lib/utils'
import { ThemeToggle } from '../components/common/ThemeToggle'

const navItems: { to: string; label: string; icon: LucideIcon; end?: boolean; adminOnly?: boolean }[] = [
  { to: '/', label: 'Dashboard', icon: LayoutDashboard, end: true },
  { to: '/upload', label: 'Sənəd yüklə', icon: FileUp },
  { to: '/study', label: 'Öyrənmə köməkçisi', icon: Sparkles },
  { to: '/questions', label: 'Sual bankı', icon: Library },
  { to: '/exams/new', label: 'İmtahana başla', icon: ListChecks },
  { to: '/battle', label: 'Battle', icon: Swords, end: true },
  { to: '/battle/rooms', label: 'Otaqlarım', icon: DoorOpen },
  { to: '/friends', label: 'Dostlar', icon: Users },
  { to: '/groups', label: 'Qruplarım', icon: GraduationCap },
  { to: '/admin', label: 'Admin Panel', icon: ShieldCheck, adminOnly: true },
  { to: '/exams', label: 'Tarixçə', icon: History, end: true },
]

export function AppLayout() {
  const { data: user } = useCurrentUser()
  const [mobileOpen, setMobileOpen] = useState(false)

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

      <aside
        className={cn(
          'app-sidebar border-slate-200 bg-white lg:sticky lg:top-0 lg:flex lg:h-dvh lg:w-64 lg:shrink-0 lg:flex-col lg:border-r',
          mobileOpen ? 'block border-b' : 'hidden',
        )}
      >
        <div className="hidden h-16 items-center justify-between px-6 lg:flex">
          <Brand />
        </div>
        <nav aria-label="Əsas naviqasiya" className="app-navigation flex min-h-0 flex-1 flex-col gap-1 overflow-y-auto p-3">
          {navItems.filter(item => !item.adminOnly || user?.roles.includes('Admin')).map(({ to, label, icon: Icon, end }) => (
            <NavLink
              key={to}
              to={to}
              end={end}
              onClick={() => setMobileOpen(false)}
              className={({ isActive }) =>
                cn(
                  'app-nav-link flex min-h-11 items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-medium transition-colors duration-150',
                  isActive ? 'bg-indigo-50 text-indigo-700' : 'text-slate-600 hover:bg-slate-100 hover:text-slate-900',
                )
              }
            >
              <Icon className="size-4" aria-hidden />
              {label}
            </NavLink>
          ))}
        </nav>
        <UserMenu />
      </aside>

      <div className="min-w-0 flex-1">
        <header className="app-toolbar hidden h-[72px] items-center justify-between border-b border-slate-200 px-8 lg:flex">
          <div className="flex items-center gap-2 text-xs"><span className="font-semibold text-slate-700">ExamR</span><span className="text-slate-400" aria-hidden>/</span><span className="text-slate-500">Şəxsi məkan</span></div>
          <div className="flex items-center gap-3"><ThemeToggle /><NotificationBell /></div>
        </header>
        <main className="app-content mx-auto max-w-[1200px] min-w-0 px-4 py-6 sm:px-6 lg:px-8 lg:py-8">
          <Outlet />
        </main>
      </div>
    </div>
  )
}

function UserMenu() {
  const { data: user } = useCurrentUser()
  const logout = useLogout()
  if (!user) return null
  const nickname = user.nickname || user.displayName || 'İstifadəçi'

  return (
    <div className="flex items-center gap-3 border-t border-slate-200 p-4 lg:mt-auto lg:shrink-0">
      <NavLink to="/profile" aria-label="Profili redaktə et" title="Profili redaktə et" className="shrink-0 rounded-full focus-visible:outline-2 focus-visible:outline-indigo-600">
        {user.avatarUrl ? <img src={user.avatarUrl} alt="" className="size-8 rounded-full object-cover" /> :
          <span className="flex size-8 items-center justify-center rounded-full bg-indigo-100 font-semibold text-indigo-700" aria-hidden>{nickname.charAt(0).toUpperCase()}</span>}
      </NavLink>
      <span className="min-w-0 flex-1">
        <NavLink to="/profile" className="block truncate font-medium hover:text-indigo-700">{nickname}</NavLink>
        <span className="block truncate text-xs text-slate-500">{user.email}</span>
      </span>
      <button
        type="button"
        onClick={() => logout.mutate()}
        disabled={logout.isPending}
        className="rounded-lg p-2 text-slate-500 hover:bg-slate-100 hover:text-slate-900"
        aria-label="Çıxış"
        title="Çıxış"
      >
        <LogOut className="size-4" />
      </button>
    </div>
  )
}

function Brand() {
  return (
    <span className="flex items-center gap-2 text-base font-semibold">
      <img src="/favicon.svg" alt="" className="size-7" />
      ExamR
    </span>
  )
}
