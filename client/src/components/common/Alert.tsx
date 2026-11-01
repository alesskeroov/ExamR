import { AlertCircle, Info } from 'lucide-react'
import type { ReactNode } from 'react'
import { cn } from '../../lib/utils'

const tones = {
  danger: { box: 'border-rose-200 bg-rose-50 text-rose-800', icon: AlertCircle },
  info: { box: 'border-indigo-100 bg-indigo-50 text-indigo-900', icon: Info },
}

interface AlertProps {
  tone?: keyof typeof tones
  title: string
  children?: ReactNode
  action?: ReactNode
}

export function Alert({ tone = 'danger', title, children, action }: AlertProps) {
  const { box, icon: Icon } = tones[tone]
  return (
    <div role={tone === 'danger' ? 'alert' : 'status'} className={cn('flex flex-col gap-3 rounded-xl border px-4 py-3 sm:flex-row sm:items-center', box)}>
      <div className="flex flex-1 gap-3">
        <Icon className="mt-0.5 size-5 shrink-0" aria-hidden />
        <div>
          <p className="font-semibold">{title}</p>
          {children && <div className="mt-0.5">{children}</div>}
        </div>
      </div>
      {action}
    </div>
  )
}
