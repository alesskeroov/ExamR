import { BadgeCheck } from 'lucide-react'
import { cn } from '../../lib/utils'

/** Green check shown after the name of an account whose email is confirmed (or proven through Google sign-in). */
export function VerifiedBadge({ className }: { className?: string }) {
  return (
    <span className={cn('inline-flex shrink-0 align-[-0.15em] text-emerald-600', className)} title="Təsdiqlənmiş hesab">
      <BadgeCheck className="size-[1.05em]" aria-hidden />
      <span className="sr-only">Təsdiqlənmiş hesab</span>
    </span>
  )
}
