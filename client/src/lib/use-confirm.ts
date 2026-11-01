import { createContext, useContext } from 'react'

export interface Confirmation {
  title: string
  description: string
  confirmLabel?: string
  danger?: boolean
}

export const ConfirmContext = createContext<((options: Confirmation) => Promise<boolean>) | null>(null)

export function useConfirm() {
  const confirm = useContext(ConfirmContext)
  if (!confirm) throw new Error('useConfirm requires ConfirmProvider')
  return confirm
}
