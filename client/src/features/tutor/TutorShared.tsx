import { Loader2 } from 'lucide-react'

/** Loading placeholder shared by the study tabs. (The old whole-document "Ortaq cavab" view is replaced by Hazırlaş → İmtahan cavabı.) */
export function Skeleton({ label }: { label: string }) {
  return (
    <div className="tutor-border space-y-3 rounded-2xl border bg-white p-5" role="status" aria-label={label}>
      <p className="flex items-center gap-2 text-sm text-slate-500"><Loader2 className="size-4 animate-spin" aria-hidden />{label}</p>
      <div className="h-4 w-1/3 animate-pulse rounded bg-slate-100" />
      <div className="h-4 w-full animate-pulse rounded bg-slate-100" />
      <div className="h-4 w-5/6 animate-pulse rounded bg-slate-100" />
    </div>
  )
}

/** Centered column used by the study tabs. */
export function Frame({ children }: { children: import('react').ReactNode }) {
  return <div className="mx-auto flex w-full max-w-2xl min-w-0 flex-col gap-4 px-4 py-5 sm:py-8" style={{ overflowWrap: 'anywhere' }}>{children}</div>
}
