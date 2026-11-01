import { useCallback, useEffect, useId, useRef, useState, type ReactNode } from 'react'
import { AlertTriangle, Check } from 'lucide-react'
import { ConfirmContext, type Confirmation } from '../../lib/use-confirm'
import { Button } from './Button'

export function ConfirmProvider({ children }: { children: ReactNode }) {
  const [request, setRequest] = useState<Confirmation | null>(null)
  const resolve = useRef<((answer: boolean) => void) | null>(null)
  const confirm = useCallback((options: Confirmation) => {
    if (resolve.current) return Promise.resolve(false)
    return new Promise<boolean>(done => {
      resolve.current = done
      setRequest(options)
    })
  }, [])
  const finish = useCallback((answer: boolean) => {
    resolve.current?.(answer)
    resolve.current = null
    setRequest(null)
  }, [])
  useEffect(() => () => { resolve.current?.(false); resolve.current = null }, [])

  return <ConfirmContext.Provider value={confirm}>
    {children}
    {request && <ConfirmationDialog request={request} onFinish={finish} />}
  </ConfirmContext.Provider>
}

function ConfirmationDialog({ request, onFinish }: { request: Confirmation; onFinish: (answer: boolean) => void }) {
  const dialog = useRef<HTMLDialogElement>(null)
  const titleId = useId()
  const descriptionId = useId()
  useEffect(() => {
    const element = dialog.current
    element?.showModal()
    return () => element?.close()
  }, [])
  return <dialog ref={dialog} aria-labelledby={titleId} aria-describedby={descriptionId}
    onCancel={event => { event.preventDefault(); onFinish(false) }}
    className="m-auto max-h-[85dvh] w-[calc(100%-2rem)] max-w-md overflow-y-auto rounded-2xl border border-slate-200 bg-white p-6 shadow-2xl backdrop:bg-slate-900/45 backdrop:backdrop-blur-sm">
    <div className={`mb-4 flex size-12 items-center justify-center rounded-xl ${request.danger ? 'bg-rose-50 text-rose-600' : 'bg-indigo-50 text-indigo-600'}`}>
      {request.danger ? <AlertTriangle className="size-6" aria-hidden /> : <Check className="size-6" aria-hidden />}
    </div>
    <h2 id={titleId} className="text-lg font-semibold text-slate-900">{request.title}</h2>
    <p id={descriptionId} className="mt-2 break-words text-sm leading-6 text-slate-600">{request.description}</p>
    <div className="mt-6 flex justify-end gap-3">
      <Button variant="secondary" autoFocus onClick={() => onFinish(false)}>Ləğv et</Button>
      <Button className={request.danger ? 'bg-rose-600 hover:bg-rose-700' : undefined} onClick={() => onFinish(true)}>{request.confirmLabel ?? 'Təsdiq et'}</Button>
    </div>
  </dialog>
}
