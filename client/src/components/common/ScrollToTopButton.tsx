import { ArrowUp } from 'lucide-react'
import { useEffect, useState } from 'react'
import { cn } from '../../lib/utils'
import { useTranslation } from 'react-i18next'

/** Floating "back to top" button that appears once the page has been scrolled down by `threshold` pixels. */
export function ScrollToTopButton({ threshold = 600 }: { threshold?: number }) {
  const [visible, setVisible] = useState(false)
  const { t } = useTranslation()

  useEffect(() => {
    const update = () => setVisible(window.scrollY > threshold)
    update()
    window.addEventListener('scroll', update, { passive: true })
    return () => window.removeEventListener('scroll', update)
  }, [threshold])

  return (
    <button
      type="button"
      onClick={() => window.scrollTo({ top: 0, behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth' })}
      aria-label={t('common.scrollTop')}
      title={t('common.scrollTop')}
      aria-hidden={!visible}
      tabIndex={visible ? 0 : -1}
      className={cn(
        'fixed right-4 bottom-[max(1rem,env(safe-area-inset-bottom))] z-20 grid size-12 place-items-center rounded-full border border-slate-300 bg-white text-slate-700 shadow-lg transition-all duration-200 hover:bg-slate-50 hover:text-indigo-600 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600 sm:right-6 sm:bottom-6 motion-reduce:transition-none',
        visible ? 'translate-y-0 opacity-100' : 'pointer-events-none translate-y-3 opacity-0',
      )}
    >
      <ArrowUp className="size-5" aria-hidden />
    </button>
  )
}
