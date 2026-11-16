import { useEffect, useRef, useState, type MouseEvent, type ReactNode } from 'react'
import { ArrowUpRight, Check, Menu, Moon, Plus, Sun, X } from 'lucide-react'
import { Link, useLocation } from 'react-router'
import { SUPPORT_EMAIL, supportMailto } from '../../lib/support'
import { useTheme } from '../../lib/use-theme'
import { ProductPreview } from './ProductPreview'
import './landing.css'
import { useTranslation } from 'react-i18next'
import { LanguageSwitcher } from '../../components/common/LanguageSwitcher'

const nav = [['#imkanlar', 'modes'], ['#nece-isleyir', 'steps'], ['#haqqimizda', 'why'], ['#suallar', 'faq']] as const
const YEAR = new Date().getFullYear()
const auth = (mode: 'login' | 'register') => `/?auth=${mode}`
const subjects = ['programming', 'networks', 'databases', 'security', 'os'] as const

export function LandingPage() {
  const { dark, toggleTheme } = useTheme()
  const { t } = useTranslation()
  const [scrolled, setScrolled] = useState(false)
  const [menu, setMenu] = useState(false)
  const [active, setActive] = useState('')
  const root = useRef<HTMLDivElement>(null)

  // Smooth in-page navigation; the sticky navbar offset comes from scroll-margin-top in CSS.
  function go(event: MouseEvent<HTMLAnchorElement>, href: string) {
    const target = document.querySelector(href)
    if (!target) return
    event.preventDefault()
    setMenu(false)
    const reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches
    const scroll = () => target.scrollIntoView({ behavior: reduce ? 'auto' : 'smooth', block: 'start' })
    // From the mobile sheet, wait until it has closed and released the page scroll lock.
    if (menu) setTimeout(scroll, 60)
    else scroll()
    history.replaceState(null, '', href)
  }

  // Highlight the section currently in the middle of the screen.
  useEffect(() => {
    const sections = nav.map(([href]) => document.querySelector(href)).filter((el): el is Element => !!el)
    const observer = new IntersectionObserver(entries => {
      const visible = entries.find(entry => entry.isIntersecting)
      if (visible) setActive(`#${visible.target.id}`)
      else if (window.scrollY < 200) setActive('')
    }, { rootMargin: '-45% 0px -50% 0px' })
    sections.forEach(el => observer.observe(el))
    return () => observer.disconnect()
  }, [])

  // Mobile sheet: lock the page behind it and close on Escape.
  useEffect(() => {
    if (!menu) return
    const overflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    const close = (event: KeyboardEvent) => { if (event.key === 'Escape') setMenu(false) }
    window.addEventListener('keydown', close)
    return () => { document.body.style.overflow = overflow; window.removeEventListener('keydown', close) }
  }, [menu])

  useEffect(() => {
    const onScroll = () => setScrolled(window.scrollY > 8)
    onScroll()
    window.addEventListener('scroll', onScroll, { passive: true })
    return () => window.removeEventListener('scroll', onScroll)
  }, [])

  // Reveal sections once as they enter the viewport.
  useEffect(() => {
    const items = root.current?.querySelectorAll('[data-reveal]') ?? []
    if (!('IntersectionObserver' in window)) { items.forEach(el => el.classList.add('is-visible')); return }
    const observer = new IntersectionObserver(entries => entries.forEach(entry => {
      if (entry.isIntersecting) { entry.target.classList.add('is-visible'); observer.unobserve(entry.target) }
    }), { rootMargin: '0px 0px -10% 0px' })
    items.forEach(el => observer.observe(el))
    root.current?.setAttribute('data-reveal-on', '')
    return () => observer.disconnect()
  }, [])

  return (
    <div ref={root} className="lp" data-theme={dark ? 'dark' : 'light'}>
      <a href="#main" className="sr-only focus:not-sr-only">{t('landing.skip')}</a>
      <header className="lp-nav" data-scrolled={scrolled || menu} data-menu={menu}>
        <div className="lp-wrap lp-nav-inner">
          <Logo />
          <nav className="lp-navlinks" aria-label={t('landing.sections')}>{nav.map(([href, id]) => <a key={href} href={href} onClick={e => go(e, href)} aria-current={active === href ? 'true' : undefined}>{t(`landing.nav.${id}`)}</a>)}</nav>
          <div className="lp-nav-actions">
            <LanguageSwitcher bare className="lp-icon-btn lp-lang" />
            {/* Desktop: theme as a quiet icon next to the account actions. On phones it lives inside the menu. */}
            <button type="button" className="lp-icon-btn lp-theme-desktop" onClick={toggleTheme} aria-pressed={dark} aria-label={dark ? t('common.theme.toLight') : t('common.theme.toDark')}>
              {dark ? <Sun className="size-[18px]" aria-hidden /> : <Moon className="size-[18px]" aria-hidden />}
            </button>
            <Link viewTransition to={auth('login')} className="lp-btn lp-btn-quiet lp-nav-login">{t('landing.login')}</Link>
            <Link viewTransition to={auth('register')} className="lp-btn lp-btn-primary lp-nav-register">{t('landing.register')}</Link>
            <button type="button" className="lp-icon-btn lp-menu-btn" onClick={() => setMenu(m => !m)} aria-expanded={menu} aria-controls="lp-sheet" aria-label={menu ? t('nav.closeMenu') : t('nav.openMenu')}>
              {menu ? <X className="size-5" aria-hidden /> : <Menu className="size-5" aria-hidden />}
            </button>
          </div>
        </div>
      </header>
      {/* Outside the header: its backdrop-filter would make it the containing block of this fixed sheet. */}
      <div id="lp-sheet" className="lp-sheet" data-open={menu} inert={!menu}>
        <nav aria-label={t('landing.sections')} className="lp-sheet-links">
          {nav.map(([href, id]) => <a key={href} href={href} onClick={e => go(e, href)} aria-current={active === href ? 'true' : undefined}>
            <span className="min-w-0"><span className="block">{t(`landing.nav.${id}`)}</span><small>{t(`landing.navHint.${id}`)}</small></span>
            <ArrowUpRight className="size-5 shrink-0" aria-hidden />
          </a>)}
        </nav>
        <div className="lp-sheet-bottom">
          <div className="lp-theme-switch" role="radiogroup" aria-label={t('landing.appearance')}>
            <span className="lp-theme-switch-label">{t('landing.appearance')}</span>
            <div>
              <button type="button" role="radio" aria-checked={!dark} onClick={() => dark && toggleTheme()}><Sun className="size-4" aria-hidden />{t('landing.light')}</button>
              <button type="button" role="radio" aria-checked={dark} onClick={() => !dark && toggleTheme()}><Moon className="size-4" aria-hidden />{t('landing.dark')}</button>
            </div>
          </div>
          <div className="lp-sheet-actions">
            <Link viewTransition to={auth('login')} className="lp-btn lp-btn-quiet">{t('landing.login')}</Link>
            <Link viewTransition to={auth('register')} className="lp-btn lp-btn-primary">{t('landing.register')}</Link>
          </div>
        </div>
      </div>

      <main id="main">
        <section className="lp-hero" aria-labelledby="hero-title">
          <div className="lp-wrap lp-hero-grid">
            <div>
              <h1 id="hero-title" className="lp-h1">{t('landing.hero.title')}</h1>
              <p className="lp-lead">{t('landing.hero.lead')}</p>
              <div className="lp-cta">
                <Link viewTransition to={auth('register')} className="lp-btn lp-btn-primary">{t('landing.hero.cta')}</Link>
                <a href="#imkanlar" onClick={e => go(e, '#imkanlar')} className="lp-btn lp-btn-quiet">{t('landing.hero.seeModes')}</a>
              </div>
              <ul className="lp-facts" aria-label={t('landing.hero.factsAria')}>
                {(['language', 'files', 'free'] as const).map(f => <li key={f}><Check className="size-3.5" strokeWidth={3} aria-hidden />{t(`landing.hero.facts.${f}`)}</li>)}
              </ul>
              <div className="lp-subjects">
                <p>{t('landing.hero.subjectsLead')}</p>
                <ul aria-label={t('landing.hero.subjectsAria')}>{subjects.map(s => <li key={s}>{t(`landing.hero.subjects.${s}`)}</li>)}</ul>
              </div>
            </div>
            <ProductPreview />
          </div>
        </section>

        <section id="imkanlar" className="lp-section" aria-labelledby="features-title">
          <div className="lp-wrap">
            <div className="lp-section-head" data-reveal>
              <h2 id="features-title" className="lp-h2">{t('landing.features.title')}</h2>
              <p className="lp-muted text-[16px] leading-7">{t('landing.features.lead')}</p>
            </div>
            <FeatureTabs />
            <div className="lp-pair" data-reveal>
              <div><h3>{t('landing.features.libraryTitle')}</h3><p className="lp-muted mt-2 leading-7">{t('landing.features.libraryText')}</p></div>
              <div><h3>{t('landing.features.friendsTitle')}</h3><p className="lp-muted mt-2 leading-7">{t('landing.features.friendsText')}</p></div>
            </div>
          </div>
        </section>

        <section id="nece-isleyir" className="lp-section" aria-labelledby="how-title">
          <div className="lp-wrap">
            <div className="lp-section-head" data-reveal>
              <h2 id="how-title" className="lp-h2">{t('landing.how.title')}</h2>
              <p className="lp-muted text-[16px] leading-7">{t('landing.how.lead')}</p>
            </div>
            <ol className="lp-track">
              <Station n="01" title={t('landing.how.s1Title')} text={t('landing.how.s1Text')}>
                <div className="lp-mini p-3.5"><div className="lp-steps-row"><span className="lp-dot" />{t('landing.how.uploaded')}<span className="lp-faint">→</span><span className="lp-dot" />{t('landing.how.processing')}<span className="lp-faint">→</span><b className="lp-mono">{t('landing.questionsCount', { count: 48 })}</b></div></div>
              </Station>
              <Station n="02" title={t('landing.how.s2Title')} text={t('landing.how.s2Text')}>
                <div className="lp-mini flex gap-1 p-1.5 text-[12.5px] font-semibold">
                  {[t('landing.how.modeExam'), t('landing.how.modeLearn'), 'Battle'].map((m, i) => <span key={m} className="flex-1 rounded-lg py-2 text-center" style={i === 0 ? { background: 'var(--lp-ink)', color: 'var(--lp-bg)' } : { color: 'var(--lp-muted)' }}>{m}</span>)}
                </div>
              </Station>
              <Station n="03" title={t('landing.how.s3Title')} text={t('landing.how.s3Text')}>
                <div className="lp-mini p-3.5"><Spark /></div>
              </Station>
            </ol>
          </div>
        </section>

        <section id="haqqimizda" className="lp-section" aria-labelledby="about-title">
          <div className="lp-wrap lp-about" data-reveal>
            <h2 id="about-title" className="lp-statement">{t('landing.about.title')}</h2>
            <div className="lp-about-body lp-muted text-[16px] leading-7">
              <p>{t('landing.about.p1')}</p>
              <p>{t('landing.about.p2')}</p>
            </div>
          </div>
        </section>

        <section id="suallar" className="lp-section" aria-labelledby="faq-title">
          <div className="lp-wrap lp-faq-wrap">
            <h2 id="faq-title" className="lp-h2" data-reveal>{t('landing.faq.title')}</h2>
            <div className="lp-faq">
              <Faq q={t('landing.faq.q1')}>{t('landing.faq.a1')}</Faq>
              <Faq q={t('landing.faq.q2')}>{t('landing.faq.a2')}</Faq>
              <Faq q={t('landing.faq.q3')}>{t('landing.faq.a3')}</Faq>
              <Faq q={t('landing.faq.q4')}>{t('landing.faq.a4')}</Faq>
              <Faq q={t('landing.faq.q5')}>{t('landing.faq.a5')}</Faq>
              <Faq q={t('landing.faq.q6')}>{t('landing.faq.a6Before')} <a href={`mailto:${SUPPORT_EMAIL}`} className="font-semibold underline underline-offset-2">{SUPPORT_EMAIL}</a> {t('landing.faq.a6After')}</Faq>
            </div>
          </div>
        </section>

        <section className="lp-wrap lp-close-section" aria-label={t('landing.close.aria')}>
          <div className="lp-close" data-reveal>
            <p className="lp-close-pill">{t('landing.close.pill')}</p>
            <h2 className="lp-close-title">{t('landing.close.title')}</h2>
            <p className="lp-close-text">{t('landing.close.text')}</p>
            <div className="lp-close-actions">
              <Link viewTransition to={auth('register')} className="lp-btn lp-btn-primary">{t('landing.close.start')}</Link>
              <Link viewTransition to={auth('login')} className="lp-btn lp-close-secondary">{t('landing.login')}</Link>
            </div>
            <p className="lp-close-note">{t('landing.close.note')}</p>
          </div>
        </section>
      </main>

      <footer className="lp-footer">
        <div className="lp-wrap">
          <div className="lp-footer-grid">
            <div className="lp-footer-brand">
              <Logo />
              <p>{t('landing.footer.tagline')}</p>
              <span className="lp-footer-badge"><span aria-hidden className="lp-footer-dot" />{t('landing.footer.badge')}</span>
            </div>
            <nav aria-label={t('landing.footer.product')} className="lp-footer-col">
              <p className="lp-footer-heading">{t('landing.footer.product')}</p>
              {nav.map(([href, id]) => <a key={href} href={href} onClick={e => go(e, href)}>{t(`landing.nav.${id}`)}</a>)}
            </nav>
            <nav aria-label={t('landing.footer.account')} className="lp-footer-col">
              <p className="lp-footer-heading">{t('landing.footer.account')}</p>
              <Link viewTransition to={auth('register')}>{t('landing.register')}</Link>
              <Link viewTransition to={auth('login')}>{t('landing.login')}</Link>
            </nav>
            <nav aria-label={t('landing.footer.contact')} className="lp-footer-col">
              <p className="lp-footer-heading">{t('landing.footer.contact')}</p>
              <a href={supportMailto({ topic: t('common.support.topic') })} className="break-words">{SUPPORT_EMAIL}</a>
              <span className="text-[13px]">{t('landing.footer.contactHint')}</span>
            </nav>
          </div>
          <div className="lp-footer-bottom">
            <span>{t('landing.footer.rights', { year: YEAR })}</span>
            <span>{t('landing.footer.author')} <b>Rəvan Ələsgərov</b></span>
          </div>
        </div>
      </footer>
    </div>
  )
}

/** On the landing page itself the logo scrolls back to the top, like most sites. */
function Logo() {
  const { pathname, search } = useLocation()
  const { t } = useTranslation()
  return <Link viewTransition to="/" className="lp-logo" aria-label={t('common.homeAria')}
    onClick={event => {
      if (pathname !== '/' || search) return
      event.preventDefault()
      window.scrollTo({ top: 0, behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth' })
      window.history.replaceState(null, '', '/')
    }}>
    <span className="lp-logo-mark" aria-hidden><svg viewBox="0 0 16 16" className="size-4"><path d="M4 4h8M4 8h5.5M4 12h8" stroke="currentColor" strokeWidth="2" strokeLinecap="round" /></svg></span>
    ExamR
  </Link>
}

function Station({ n, title, text, children }: { n: string; title: string; text: string; children: ReactNode }) {
  return <li className="lp-station" data-reveal>
    <span className="lp-station-num" aria-hidden>{n}</span>
    <h3>{title}</h3>
    <p className="lp-muted mt-2 leading-7">{text}</p>
    <div className="lp-station-visual" aria-hidden>{children}</div>
  </li>
}

function Faq({ q, children }: { q: string; children: ReactNode }) {
  return <details><summary>{q}<Plus className="lp-plus size-5" aria-hidden /></summary><p>{children}</p></details>
}

function Spark() {
  // Illustrative trend: what the dashboard chart looks like, not real data.
  const points = [42, 55, 51, 63, 70, 68, 81]
  const path = points.map((v, i) => `${i * 40},${60 - v * 0.6}`).join(' ')
  return <div className="flex items-end justify-between gap-3">
    <svg viewBox="0 0 240 60" className="h-12 w-full" preserveAspectRatio="none"><polyline points={path} fill="none" stroke="var(--lp-accent)" strokeWidth="2.5" strokeLinejoin="round" strokeLinecap="round" vectorEffect="non-scaling-stroke" /></svg>
    <span className="lp-mono shrink-0 text-[20px] font-bold">81%</span>
  </div>
}

const features = [{ id: 'doc' }, { id: 'exam' }, { id: 'learn' }, { id: 'battle' }, { id: 'stats' }] as const

function FeatureTabs() {
  const [active, setActive] = useState<(typeof features)[number]['id']>('doc')
  const { t } = useTranslation()
  return <div className="lp-features" data-reveal>
    <div className="lp-tabs" role="tablist" aria-label={t('landing.features.tabsAria')} onKeyDown={e => {
      if (e.key !== 'ArrowDown' && e.key !== 'ArrowUp' && e.key !== 'ArrowRight' && e.key !== 'ArrowLeft') return
      e.preventDefault()
      const i = features.findIndex(f => f.id === active)
      const next = features[(i + (e.key === 'ArrowDown' || e.key === 'ArrowRight' ? 1 : features.length - 1)) % features.length]
      setActive(next.id)
      document.getElementById(`lp-tab-${next.id}`)?.focus()
    }}>
      {features.map(f => <button key={f.id} id={`lp-tab-${f.id}`} type="button" role="tab" className="lp-tab" aria-selected={active === f.id} aria-controls="lp-feature-panel" tabIndex={active === f.id ? 0 : -1} onClick={() => setActive(f.id)}>
        {t(`landing.tabs.${f.id}.label`)}<small>{t(`landing.tabs.${f.id}.hint`)}</small>
      </button>)}
    </div>
    <div id="lp-feature-panel" role="tabpanel" aria-labelledby={`lp-tab-${active}`} className="lp-panel">
      <div key={active} className="lp-panel-inner mx-auto w-full max-w-[520px]"><FeaturePanel id={active} /></div>
    </div>
  </div>
}

function FeaturePanel({ id }: { id: (typeof features)[number]['id'] }) {
  const { t } = useTranslation()
  const panels: Record<(typeof features)[number]['id'], ReactNode> = {
  doc: <div className="grid gap-3">
    <div className="lp-mini flex items-center gap-3 p-4"><span className="lp-chip">PDF</span><span className="min-w-0 flex-1 truncate font-semibold">komputer-sebekeleri.pdf</span><span className="lp-mono text-[12px] lp-muted">2.4 MB</span></div>
    <div className="lp-mini p-4">
      <div className="flex items-baseline justify-between"><span className="font-semibold">{t('landing.panel.processed')}</span><span className="lp-mono text-[12px] lp-muted">{t('landing.panel.seconds', { n: 12 })}</span></div>
      <div className="lp-bar mt-3"><i style={{ width: '100%' }} /></div>
      <dl className="mt-4 grid grid-cols-3 gap-2 text-center">
        {[['64', t('landing.panel.questions')], ['61', t('landing.panel.answered')], ['3', t('landing.panel.toReview')]].map(([v, k]) => <div key={k} className="rounded-lg py-2" style={{ background: 'var(--lp-sunken)' }}><dt className="lp-mono text-[18px] font-bold">{v}</dt><dd className="text-[12px] lp-muted">{k}</dd></div>)}
      </dl>
    </div>
    <p className="text-center text-[12px] lp-muted">{t('landing.panel.sampleResult')}</p>
  </div>,
  exam: <div className="lp-mini overflow-hidden">
    <div className="lp-examcard-head"><span className="lp-mono">{t('landing.panel.questionOf', { n: 12, total: 30 })}</span><span className="lp-timer lp-mono">21:05</span></div>
    <div className="p-4"><p className="lp-q">{t('landing.panel.examQ')}</p>
      <div className="lp-options"><div className="lp-option" data-state="picked"><b>A</b>HTML</div><div className="lp-option"><b>B</b>SQL</div><div className="lp-option"><b>C</b>Python</div></div></div>
    <div className="flex items-center justify-between border-t px-4 py-3 text-[12.5px] lp-muted" style={{ borderColor: 'var(--lp-line)' }}><span>{t('landing.panel.range', { range: '1–120' })}</span><span>{t('landing.panel.answersAtEnd')}</span></div>
  </div>,
  learn: <div className="lp-mini p-5">
    <p className="text-[12px] lp-muted">{t('landing.panel.learnHead')}</p>
    <p className="lp-q mt-1">{t('landing.panel.learnQ')}</p>
    <div className="mt-3 rounded-lg p-3 text-[13.5px] leading-6" style={{ background: 'var(--lp-sunken)' }}>{t('landing.panel.learnA')}</div>
    <div className="mt-4 flex items-center gap-3"><div className="lp-bar flex-1"><i style={{ width: '72%' }} /></div><span className="lp-mono text-[13px] font-bold">72%</span></div>
    <p className="mt-2 text-[12.5px] lp-muted">{t('landing.panel.missing')} <b style={{ color: 'var(--lp-ink)' }}>{t('landing.panel.missingTerm')}</b></p>
  </div>,
  battle: <div className="lp-mini p-5">
    <div className="flex items-center justify-between"><span className="font-semibold">{t('landing.panel.room', { n: 4 })}</span><span className="lp-chip">{t('landing.panel.live')}</span></div>
    <ul className="mt-4 grid gap-2.5">
      {[['Aysel', 86], [t('landing.panel.you'), 79], ['Murad', 64], ['Nigar', 41]].map(([n, v]) => <li key={n} className="grid grid-cols-[64px_1fr_36px] items-center gap-3 text-[13.5px]">
        <span className="font-semibold" style={n === t('landing.panel.you') ? { color: 'var(--lp-accent)' } : undefined}>{n}</span><div className="lp-bar"><i style={{ width: `${v}%`, opacity: n === t('landing.panel.you') ? 1 : .45 }} /></div><span className="lp-mono text-right">{v}</span>
      </li>)}
    </ul>
    <p className="mt-4 rounded-lg px-3 py-2 text-[13px]" style={{ background: 'var(--lp-sunken)' }}><b>Murad:</b> {t('landing.panel.chat')}</p>
  </div>,
  stats: <div className="grid gap-3 sm:grid-cols-[1.4fr_1fr]">
    <div className="lp-mini p-4"><p className="text-[12px] lp-muted">{t('landing.panel.last7')}</p><div className="mt-3"><Spark /></div></div>
    <div className="lp-mini p-4"><p className="text-[12px] lp-muted">{t('landing.panel.focusWeek')}</p><p className="lp-mono mt-2 text-[26px] font-bold">{t('landing.panel.focusTime')}</p>
      <div className="mt-2 flex h-10 items-end gap-1">{[30, 55, 20, 70, 45, 85, 60].map((h, i) => <i key={i} className="flex-1 rounded-sm" style={{ height: `${h}%`, background: 'var(--lp-accent)', opacity: .35 + h / 200 }} />)}</div></div>
    <p className="text-center text-[12px] lp-muted sm:col-span-2">{t('landing.panel.sampleStats')}</p>
  </div>,
  }
  return panels[id]
}
