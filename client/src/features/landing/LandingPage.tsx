import { useEffect, useRef, useState, type MouseEvent, type ReactNode } from 'react'
import { ArrowRight, Menu, Moon, Plus, Sun, X } from 'lucide-react'
import { Link } from 'react-router'
import { useTheme } from '../../lib/use-theme'
import { ProductPreview } from './ProductPreview'
import './landing.css'

const nav = [['#imkanlar', 'Rejimlər'], ['#nece-isleyir', 'Addımlar'], ['#haqqimizda', 'Niyə ExamR'], ['#suallar', 'Suallar']] as const
const YEAR = new Date().getFullYear()
const auth = (mode: 'login' | 'register') => `/?auth=${mode}`

export function LandingPage() {
  const { dark, toggleTheme } = useTheme()
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
      <a href="#main" className="sr-only focus:not-sr-only">Məzmuna keç</a>
      <header className="lp-nav" data-scrolled={scrolled || menu} data-menu={menu}>
        <div className="lp-wrap lp-nav-inner">
          <Logo />
          <nav className="lp-navlinks" aria-label="Bölmələr">{nav.map(([href, label]) => <a key={href} href={href} onClick={e => go(e, href)} aria-current={active === href ? 'true' : undefined}>{label}</a>)}</nav>
          <div className="lp-nav-actions">
            <button type="button" className="lp-icon-btn" onClick={toggleTheme} aria-pressed={dark} aria-label={dark ? 'İşıqlı rejimə keç' : 'Qaranlıq rejimə keç'}>
              {dark ? <Sun className="size-[18px]" aria-hidden /> : <Moon className="size-[18px]" aria-hidden />}
            </button>
            <Link viewTransition to={auth('login')} className="lp-btn lp-btn-quiet lp-nav-login">Daxil ol</Link>
            <Link viewTransition to={auth('register')} className="lp-btn lp-btn-primary">Qeydiyyat</Link>
            <button type="button" className="lp-icon-btn lp-menu-btn" onClick={() => setMenu(m => !m)} aria-expanded={menu} aria-controls="lp-sheet" aria-label={menu ? 'Menyunu bağla' : 'Menyunu aç'}>
              {menu ? <X className="size-5" aria-hidden /> : <Menu className="size-5" aria-hidden />}
            </button>
          </div>
        </div>
      </header>
      {/* Outside the header: its backdrop-filter would make it the containing block of this fixed sheet. */}
      <div id="lp-sheet" className="lp-sheet" data-open={menu} inert={!menu}>
        <nav aria-label="Bölmələr">
          {nav.map(([href, label], i) => <a key={href} href={href} onClick={e => go(e, href)} aria-current={active === href ? 'true' : undefined}>
            <span className="lp-mono">0{i + 1}</span>{label}
          </a>)}
        </nav>
        <div className="lp-sheet-actions">
          <Link viewTransition to={auth('login')} className="lp-btn lp-btn-quiet">Daxil ol</Link>
          <Link viewTransition to={auth('register')} className="lp-btn lp-btn-primary">Qeydiyyat</Link>
        </div>
      </div>

      <main id="main">
        <section className="lp-hero" aria-labelledby="hero-title">
          <div className="lp-wrap lp-hero-grid">
            <div>
              <p className="lp-eyebrow">PDF · DOCX → imtahan</p>
              <h1 id="hero-title" className="lp-h1 mt-4">Konspektin artıq <span className="lp-mark">imtahandır.</span></h1>
              <p className="lp-lead">Sual-cavab sənədini yüklə — ExamR sualları özü çıxarır. Vaxtlı imtahan ver, öyrənmə rejimində təkrar et, dostunla yarış. Nə öyrəndiyini rəqəmlərlə gör.</p>
              <div className="lp-cta">
                <Link viewTransition to={auth('register')} className="lp-btn lp-btn-primary">İlk faylını yüklə <ArrowRight className="lp-arrow size-4" aria-hidden /></Link>
                <a href="#imkanlar" onClick={e => go(e, '#imkanlar')} className="lp-btn lp-btn-quiet">Rejimlərə bax</a>
              </div>
              <div className="lp-facts"><span>Azərbaycan dilində</span><span>PDF və Word</span><span>Hazırda demo mərhələsində</span></div>
            </div>
            <ProductPreview />
          </div>
        </section>

        <section id="imkanlar" className="lp-section" aria-labelledby="features-title">
          <div className="lp-wrap">
            <div className="lp-section-head" data-reveal>
              <h2 id="features-title" className="lp-h2">Bir fayl yükləyirsən — beş fərqli məşq alırsan</h2>
              <p className="lp-muted text-[16px] leading-7">Yüklədiyin hər fayl şəxsi kitabxanana düşür və aşağıdakı rejimlərin hamısında istifadə olunur. Ayrıca sual yazmağa ehtiyac yoxdur.</p>
            </div>
            <FeatureTabs />
            <div className="lp-pair" data-reveal>
              <div><h3>Şəxsi kitabxana</h3><p className="lp-muted mt-2 leading-7">Sənədlərin yalnız sənə görünür. Sual bankında axtar, filtrlə, səhv çıxarılmış sualı düzəlt və vacib olanları seçilmişlərə at.</p></div>
              <div><h3>Dostlar və qruplar</h3><p className="lp-muted mt-2 leading-7">Dostlarını nickname ilə tap, onlayn olanları gör və bir kliklə Battle-a dəvət et. Müəllim qrupa material paylaşanda o, sənin siyahında görünür.</p></div>
            </div>
          </div>
        </section>

        <section id="nece-isleyir" className="lp-section" aria-labelledby="how-title">
          <div className="lp-wrap">
            <div className="lp-section-head" data-reveal>
              <h2 id="how-title" className="lp-h2">Fayldan ilk nəticəyə qədər</h2>
              <p className="lp-muted text-[16px] leading-7">Adi sual-cavab faylı üçün bu, adətən bir neçə dəqiqə çəkir.</p>
            </div>
            <ol className="lp-track">
              <Station n="01" title="Sənədini yüklə" text="Mətnli PDF və ya DOCX — sualları, variantları və düzgün cavabları avtomatik tanıyır.">
                <div className="lp-mini p-3.5"><div className="lp-steps-row"><span className="lp-dot" />yükləndi<span className="lp-faint">→</span><span className="lp-dot" />emal<span className="lp-faint">→</span><b className="lp-mono">48 sual</b></div></div>
              </Station>
              <Station n="02" title="Rejimi seç" text="Vaxtlı imtahan, addım-addım öyrənmə, səhvlərin təkrarı və ya canlı Battle.">
                <div className="lp-mini flex gap-1 p-1.5 text-[12.5px] font-semibold">
                  {['İmtahan', 'Öyrənmə', 'Battle'].map((m, i) => <span key={m} className="flex-1 rounded-lg py-2 text-center" style={i === 0 ? { background: 'var(--lp-ink)', color: 'var(--lp-bg)' } : { color: 'var(--lp-muted)' }}>{m}</span>)}
                </div>
              </Station>
              <Station n="03" title="Nəticəni izlə" text="Orta faiz, zəif mövzular və fokus vaxtı — hər sessiyadan sonra yenilənir.">
                <div className="lp-mini p-3.5"><Spark /></div>
              </Station>
            </ol>
          </div>
        </section>

        <section id="haqqimizda" className="lp-section" aria-labelledby="about-title">
          <div className="lp-wrap lp-about" data-reveal>
            <h2 id="about-title" className="lp-statement">Hazırlaşmaq üçün materialın çoxdur. Çatışmayan — onu yoxlamağın rahat yoludur.</h2>
            <div className="lp-about-body lp-muted text-[16px] leading-7">
              <p>ExamR bir tələbə problemindən yarandı: müəllimlər sual-cavab faylları paylayır, amma onlarla işləmək kağızdan oxumaqdan o yana keçmir.</p>
              <p>Biz o faylı interaktiv məşqə çeviririk. Özünü sınayırsan, səhvlərini görürsən və növbəti dəfə nəyə vaxt ayıracağını bilirsən. Məqsəd sadədir: daha az vaxt, daha dəqiq hazırlıq.</p>
            </div>
          </div>
        </section>

        <section id="suallar" className="lp-section" aria-labelledby="faq-title">
          <div className="lp-wrap lp-faq-wrap">
            <h2 id="faq-title" className="lp-h2" data-reveal>Tez-tez verilən suallar</h2>
            <div className="lp-faq">
              <Faq q="Hansı faylları yükləyə bilərəm?">Mətn əsaslı PDF və Word (.docx) faylları, 20 MB-a qədər. Skan edilmiş şəkil formatlı PDF-lərdən mətn oxunmur.</Faq>
              <Faq q="Sənədlərimi başqaları görə bilər?">Xeyr. Yüklədiyin sənədlər yalnız sənə görünür. Paylaşma yalnız admin tərəfindən, qrup və ya bütün istifadəçilər üçün edilir.</Faq>
              <Faq q="Sual səhv tanınıbsa nə edim?">Sual bankında öz sualını redaktə edə və “yoxlanılıb” kimi təsdiqləyə bilərsən. Dəyişiklik növbəti imtahanlarda nəzərə alınır.</Faq>
              <Faq q="ExamR ödənişlidir?">Hazırda ExamR demo mərhələsindədir və pulsuzdur. Bu dövrdə bəzi funksiyalar dəyişə bilər.</Faq>
              <Faq q="Google hesabı ilə daxil ola bilərəm?">Bəli. Eyni email ilə artıq hesabın varsa, əvvəl şifrə ilə daxil ol və Google-u profilindən əlaqələndir.</Faq>
            </div>
          </div>
        </section>

        <section className="lp-wrap pb-16" aria-label="Başla">
          <div className="lp-close" data-reveal>
            <div>
              <p className="lp-eyebrow" style={{ color: 'inherit', opacity: .6 }}>imtahanına neçə gün qalıb?</p>
              <p className="mt-3 text-[clamp(1.6rem,4vw,2.4rem)] font-bold leading-tight tracking-[-0.035em]">Sual-cavab faylın hazırdırsa, qalanını ExamR edir.</p>
            </div>
            <Link viewTransition to={auth('register')} className="lp-btn lp-btn-primary">Hesab aç <ArrowRight className="lp-arrow size-4" aria-hidden /></Link>
          </div>
        </section>
      </main>

      <footer className="lp-footer">
        <div className="lp-wrap lp-footer-grid">
          <div className="flex flex-wrap items-center gap-x-4 gap-y-2"><Logo /><span>© {YEAR} ExamR · demo versiya</span><span>Developer: <b className="font-semibold" style={{ color: 'var(--lp-ink)' }}>Rəvan Ələsgərov</b></span></div>
          <nav aria-label="Alt menyu">{nav.map(([href, label]) => <a key={href} href={href} onClick={e => go(e, href)}>{label}</a>)}<Link viewTransition to={auth('login')}>Daxil ol</Link></nav>
        </div>
      </footer>
    </div>
  )
}

function Logo() {
  return <Link viewTransition to="/" className="lp-logo" aria-label="ExamR ana səhifə">
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

const features = [
  { id: 'doc', label: 'Sənəddən sual', hint: 'PDF/DOCX avtomatik oxunur' },
  { id: 'exam', label: 'Sınaq imtahanı', hint: 'Vaxt, sual aralığı, nəticə' },
  { id: 'learn', label: 'Öyrənmə rejimi', hint: 'Yaz, ipucu al, müqayisə et' },
  { id: 'battle', label: 'Battle', hint: 'Dostlarla canlı yarış' },
  { id: 'stats', label: 'İnkişaf', hint: 'Faiz, səhvlər, fokus vaxtı' },
] as const

function FeatureTabs() {
  const [active, setActive] = useState<(typeof features)[number]['id']>('doc')
  return <div className="lp-features" data-reveal>
    <div className="lp-tabs" role="tablist" aria-label="ExamR imkanları" onKeyDown={e => {
      if (e.key !== 'ArrowDown' && e.key !== 'ArrowUp' && e.key !== 'ArrowRight' && e.key !== 'ArrowLeft') return
      e.preventDefault()
      const i = features.findIndex(f => f.id === active)
      const next = features[(i + (e.key === 'ArrowDown' || e.key === 'ArrowRight' ? 1 : features.length - 1)) % features.length]
      setActive(next.id)
      document.getElementById(`lp-tab-${next.id}`)?.focus()
    }}>
      {features.map(f => <button key={f.id} id={`lp-tab-${f.id}`} type="button" role="tab" className="lp-tab" aria-selected={active === f.id} aria-controls="lp-feature-panel" tabIndex={active === f.id ? 0 : -1} onClick={() => setActive(f.id)}>
        {f.label}<small>{f.hint}</small>
      </button>)}
    </div>
    <div id="lp-feature-panel" role="tabpanel" aria-labelledby={`lp-tab-${active}`} className="lp-panel">
      <div key={active} className="lp-panel-inner mx-auto w-full max-w-[520px]">{panels[active]}</div>
    </div>
  </div>
}

const panels: Record<(typeof features)[number]['id'], ReactNode> = {
  doc: <div className="grid gap-3">
    <div className="lp-mini flex items-center gap-3 p-4"><span className="lp-chip">PDF</span><span className="min-w-0 flex-1 truncate font-semibold">fizika-mexanika.pdf</span><span className="lp-mono text-[12px] lp-muted">2.4 MB</span></div>
    <div className="lp-mini p-4">
      <div className="flex items-baseline justify-between"><span className="font-semibold">Emal tamamlandı</span><span className="lp-mono text-[12px] lp-muted">12 san</span></div>
      <div className="lp-bar mt-3"><i style={{ width: '100%' }} /></div>
      <dl className="mt-4 grid grid-cols-3 gap-2 text-center">
        {[['64', 'sual'], ['61', 'cavablı'], ['3', 'yoxlanmalı']].map(([v, k]) => <div key={k} className="rounded-lg py-2" style={{ background: 'var(--lp-sunken)' }}><dt className="lp-mono text-[18px] font-bold">{v}</dt><dd className="text-[12px] lp-muted">{k}</dd></div>)}
      </dl>
    </div>
    <p className="text-center text-[12px] lp-muted">Nümunə nəticə</p>
  </div>,
  exam: <div className="lp-mini overflow-hidden">
    <div className="lp-examcard-head"><span className="lp-mono">Sual 12 / 30</span><span className="lp-timer lp-mono">21:05</span></div>
    <div className="p-4"><p className="lp-q">Sürət vahid zamanda qət edilən … ilə ölçülür.</p>
      <div className="lp-options"><div className="lp-option" data-state="picked"><b>A</b>yol</div><div className="lp-option"><b>B</b>qüvvə</div><div className="lp-option"><b>C</b>kütlə</div></div></div>
    <div className="flex items-center justify-between border-t px-4 py-3 text-[12.5px] lp-muted" style={{ borderColor: 'var(--lp-line)' }}><span>Aralıq: 1–120</span><span>Cavablar sonda göstərilir</span></div>
  </div>,
  learn: <div className="lp-mini p-5">
    <p className="text-[12px] lp-muted">Sual 4 · Məni yoxla</p>
    <p className="lp-q mt-1">Fotosintez nədir?</p>
    <div className="mt-3 rounded-lg p-3 text-[13.5px] leading-6" style={{ background: 'var(--lp-sunken)' }}>Bitkilərin işıq enerjisi ilə karbon qazı və sudan qlükoza yaratması…</div>
    <div className="mt-4 flex items-center gap-3"><div className="lp-bar flex-1"><i style={{ width: '72%' }} /></div><span className="lp-mono text-[13px] font-bold">72%</span></div>
    <p className="mt-2 text-[12.5px] lp-muted">Çatışmayan anlayış: <b style={{ color: 'var(--lp-ink)' }}>xlorofil</b></p>
  </div>,
  battle: <div className="lp-mini p-5">
    <div className="flex items-center justify-between"><span className="font-semibold">Otaq · 4 nəfər</span><span className="lp-chip">canlı</span></div>
    <ul className="mt-4 grid gap-2.5">
      {[['Aysel', 86], ['Sən', 79], ['Murad', 64], ['Nigar', 41]].map(([n, v]) => <li key={n} className="grid grid-cols-[64px_1fr_36px] items-center gap-3 text-[13.5px]">
        <span className="font-semibold" style={n === 'Sən' ? { color: 'var(--lp-accent)' } : undefined}>{n}</span><div className="lp-bar"><i style={{ width: `${v}%`, opacity: n === 'Sən' ? 1 : .45 }} /></div><span className="lp-mono text-right">{v}</span>
      </li>)}
    </ul>
    <p className="mt-4 rounded-lg px-3 py-2 text-[13px]" style={{ background: 'var(--lp-sunken)' }}><b>Murad:</b> bu sual çətin idi 😅</p>
  </div>,
  stats: <div className="grid gap-3 sm:grid-cols-[1.4fr_1fr]">
    <div className="lp-mini p-4"><p className="text-[12px] lp-muted">Son 7 imtahan</p><div className="mt-3"><Spark /></div></div>
    <div className="lp-mini p-4"><p className="text-[12px] lp-muted">Fokus · bu həftə</p><p className="lp-mono mt-2 text-[26px] font-bold">3s 40d</p>
      <div className="mt-2 flex h-10 items-end gap-1">{[30, 55, 20, 70, 45, 85, 60].map((h, i) => <i key={i} className="flex-1 rounded-sm" style={{ height: `${h}%`, background: 'var(--lp-accent)', opacity: .35 + h / 200 }} />)}</div></div>
    <p className="text-center text-[12px] lp-muted sm:col-span-2">Nümunə statistika</p>
  </div>,
}
