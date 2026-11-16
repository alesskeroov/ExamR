import { useEffect, useId, useRef, type ReactNode } from 'react'

/** Modal used by the billing admin: native dialog, Escape closes unless an action is running. */
export function AdminDialog({ title, busy, onClose, children, wide }: { title: string; busy?: boolean; onClose: () => void; children: ReactNode; wide?: boolean }) {
  const ref = useRef<HTMLDialogElement>(null)
  const titleId = useId()
  useEffect(() => { const el = ref.current!; el.showModal(); return () => el.close() }, [])
  return <dialog ref={ref} aria-labelledby={titleId} onCancel={e => { e.preventDefault(); if (!busy) onClose() }}
    className={`m-auto max-h-[calc(100dvh-2rem)] overflow-y-auto rounded-xl bg-white p-6 text-slate-900 shadow-xl backdrop:bg-slate-950/40 ${wide ? 'w-[min(48rem,calc(100%-2rem))]' : 'w-[min(32rem,calc(100%-2rem))]'}`}>
    <h2 id={titleId} className="text-lg font-semibold">{title}</h2>
    <div className="mt-4">{children}</div>
  </dialog>
}
