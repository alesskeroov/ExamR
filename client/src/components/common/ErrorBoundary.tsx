import { Component, type ErrorInfo, type ReactNode } from 'react'
import { SupportContact } from './SupportContact'

/** Last-resort fallback so a rendering error shows a recovery screen instead of a blank page. */
export class ErrorBoundary extends Component<{ children: ReactNode }, { failed: boolean }> {
  state = { failed: false }

  static getDerivedStateFromError() {
    return { failed: true }
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('Unhandled UI error', error, info.componentStack)
  }

  render() {
    if (!this.state.failed) return this.props.children
    return (
      <div role="alert" className="flex min-h-dvh flex-col items-center justify-center gap-3 px-4 text-center">
        <p className="text-lg font-semibold">Gözlənilməz xəta baş verdi</p>
        <p className="max-w-sm text-slate-600">Səhifəni yeniləyin. Problem təkrarlansa, bir az sonra yenidən cəhd edin.</p>
        <SupportContact className="max-w-sm text-slate-600" topic="Gözlənilməz xəta" />
        <button type="button" onClick={() => window.location.reload()} className="mt-2 h-10 rounded-xl btn-primary px-4 text-sm font-medium">
          Səhifəni yenilə
        </button>
      </div>
    )
  }
}
