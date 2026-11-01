import { FileUp, History, LayoutDashboard, Library, ListChecks, LogOut, Menu, Swords, X, type LucideIcon } from 'lucide-react'
import { useState } from 'react'
import { NavLink, Outlet } from 'react-router'
import { useCurrentUser, useLogout } from '../features/auth/api'
import { cn } from '../lib/utils'

const navItems: { to: string; label: string; icon: LucideIcon; end?: boolean }[] = [
  { to: '/', label: 'Dashboard', icon: LayoutDashboard, end: true },
  { to: '/upload', label: 'PDF yüklə', icon: FileUp },
  { to: '/questions', label: 'Sual bankı', icon: Library },
  { to: '/exams/new', label: 'İmtahana başla', icon: ListChecks },
  { to: '/battle', label: 'Battle', icon: Swords },
  { to: '/exams', label: 'Tarixçə', icon: History, end: true },
]

export function AppLayout() {
  const [mobileOpen, setMobileOpen] = useState(false)

  return (
    <div className="min-h-dvh lg:flex">
      {/* Mobile top bar */}
      <header className="sticky top-0 z-20 flex h-14 items-center justify-between border-b border-slate-200 bg-white px-4 lg:hidden">
        <Brand />
        <button
          type="button"
          onClick={() => setMobileOpen((open) => !open)}
          className="rounded-lg p-2 text-slate-600 hover:bg-slate-100"
          aria-label={mobileOpen ? 'Menyunu bağla' : 'Menyunu aç'}
          aria-expanded={mobileOpen}
        >
          {mobileOpen ? <X className="size-5" /> : <Menu className="size-5" />}
        </button>
      </header>

      <aside
        className={cn(
          'border-slate-200 bg-white lg:sticky lg:top-0 lg:block lg:h-dvh lg:w-64 lg:shrink-0 lg:border-r',
          mobileOpen ? 'block border-b' : 'hidden',
        )}
      >
        <div className="hidden h-16 items-center px-6 lg:flex">
          <Brand />
        </div>
        <nav aria-label="Əsas naviqasiya" className="flex flex-col gap-1 p-3">
          {navItems.map(({ to, label, icon: Icon, end }) => (
            <NavLink
              key={to}
              to={to}
              end={end}
              onClick={() => setMobileOpen(false)}
              className={({ isActive }) =>
                cn(
                  'flex items-center gap-3 rounded-lg px-3 py-2 font-medium transition-colors duration-150',
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

      <main className="mx-auto max-w-[1200px] min-w-0 flex-1 px-4 py-6 lg:px-8 lg:py-8">
        <Outlet />
      </main>
    </div>
  )
}

function UserMenu() {
  const { data: user } = useCurrentUser()
  const logout = useLogout()
  if (!user) return null

  return (
    <div className="flex items-center gap-3 border-t border-slate-200 p-4 lg:absolute lg:inset-x-0 lg:bottom-0">
      <span className="flex size-8 shrink-0 items-center justify-center rounded-full bg-indigo-100 font-semibold text-indigo-700" aria-hidden>
        {user.displayName.charAt(0).toUpperCase()}
      </span>
      <span className="min-w-0 flex-1">
        <span className="block truncate font-medium">{user.displayName}</span>
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
      ExamFlow
    </span>
  )
}
