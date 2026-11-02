import { useId } from 'react'

// Metal ramps per tier: highlight, base, shadow. The shape also escalates with the tier, so the emblem reads
// without colour too: shield (Bronze/Silver/Gold) → crest (Platinum) → cut gem (Diamond) → crown (Master).
const metals: Record<string, [string, string, string]> = {
  Bronze: ['#F0B27E', '#B8703C', '#7A4320'],
  Silver: ['#F4F7FB', '#B4BDC9', '#6E7787'],
  Gold: ['#FFE89A', '#E8AE26', '#9A6604'],
  Platinum: ['#E9FBFF', '#8FD6E6', '#2F8497'],
  Diamond: ['#E3EAFF', '#7D93FF', '#3B3FCF'],
  Master: ['#FFE7A3', '#F2B33D', '#A7650A'],
}

/** Tier emblem drawn in a 48×48 grid; stays legible from 16px badges up to large profile headers. */
export function RankEmblem({ name, size = 20, className }: { name: string; size?: number; className?: string }) {
  const id = useId().replace(/:/g, '')
  const [light, base, dark] = metals[name] ?? metals.Bronze
  return <svg viewBox="0 0 48 48" width={size} height={size} className={className} role="img" aria-label={`${name} rank`}>
    <defs>
      <linearGradient id={`${id}-m`} x1="0" y1="0" x2="0.6" y2="1">
        <stop offset="0" stopColor={light} /><stop offset=".55" stopColor={base} /><stop offset="1" stopColor={dark} />
      </linearGradient>
    </defs>
    <Shape name={metals[name] ? name : 'Bronze'} id={id} />
  </svg>
}

function Shape({ name, id }: { name: string; id: string }) {
  switch (name) {
    case 'Bronze': return <>
        <path d="M24 3L40 9V22C40 33 33 40.5 24 45C15 40.5 8 33 8 22V9Z" fill={`url(#${id}-m)`} stroke="#7A4320" strokeWidth="1.5" strokeLinejoin="round" />
        <path d="M24 7.5L36 12.2V22C36 30.6 30.7 36.6 24 40.4C17.3 36.6 12 30.6 12 22V12.2Z" fill="none" stroke="#FFE2C6" strokeOpacity=".55" strokeWidth="1.2" strokeLinejoin="round" />
        <path d="M17 21L24 26.5L31 21" fill="none" stroke="#FFE2C6" strokeWidth="3.2" strokeLinecap="round" strokeLinejoin="round" />
        <path d="M24 3L40 9V17C33 19 15 19 8 17V9Z" fill="#fff" fillOpacity=".22" />
      </>
    case 'Silver': return <>
        <path d="M24 3L40 9V22C40 33 33 40.5 24 45C15 40.5 8 33 8 22V9Z" fill={`url(#${id}-m)`} stroke="#6E7787" strokeWidth="1.5" strokeLinejoin="round" />
        <path d="M24 7.5L36 12.2V22C36 30.6 30.7 36.6 24 40.4C17.3 36.6 12 30.6 12 22V12.2Z" fill="none" stroke="#FFFFFF" strokeOpacity=".55" strokeWidth="1.2" strokeLinejoin="round" />
        <path d="M17 18L24 23.5L31 18" fill="none" stroke="#FFFFFF" strokeWidth="3.2" strokeLinecap="round" strokeLinejoin="round" />
        <path d="M17 25L24 30.5L31 25" fill="none" stroke="#FFFFFF" strokeWidth="3.2" strokeLinecap="round" strokeLinejoin="round" />
        <path d="M24 3L40 9V17C33 19 15 19 8 17V9Z" fill="#fff" fillOpacity=".22" />
      </>
    case 'Gold': return <>
        <path d="M24 3L40 9V22C40 33 33 40.5 24 45C15 40.5 8 33 8 22V9Z" fill={`url(#${id}-m)`} stroke="#9A6604" strokeWidth="1.5" strokeLinejoin="round" />
        <path d="M24 7.5L36 12.2V22C36 30.6 30.7 36.6 24 40.4C17.3 36.6 12 30.6 12 22V12.2Z" fill="none" stroke="#FFF6D6" strokeOpacity=".55" strokeWidth="1.2" strokeLinejoin="round" />
        <path d="M24.0 15.5 L26.0 20.7 L31.6 21.0 L27.2 24.6 L28.7 30.0 L24.0 26.9 L19.3 30.0 L20.8 24.6 L16.4 21.0 L22.0 20.7Z" fill="#FFF6D6" stroke="#9A6604" strokeOpacity=".35" strokeWidth=".8" strokeLinejoin="round" />
        <path d="M24 3L40 9V17C33 19 15 19 8 17V9Z" fill="#fff" fillOpacity=".22" />
      </>
    case 'Platinum': return <>
        <path d="M24.0 3.0 L42.2 13.5 L42.2 34.5 L24.0 45.0 L5.8 34.5 L5.8 13.5Z" fill={`url(#${id}-m)`} stroke="#2F8497" strokeWidth="1.5" strokeLinejoin="round" />
        <path d="M24.0 7.5 L38.3 15.8 L38.3 32.2 L24.0 40.5 L9.7 32.2 L9.7 15.7Z" fill="none" stroke="#FFFFFF" strokeOpacity=".55" strokeWidth="1.2" strokeLinejoin="round" />
        <path d="M24.0 16.8 L25.8 21.5 L30.8 21.8 L26.9 25.0 L28.2 29.8 L24.0 27.1 L19.8 29.8 L21.1 25.0 L17.2 21.8 L22.2 21.5Z" fill="#FFFFFF" stroke="#2F8497" strokeOpacity=".35" strokeWidth=".8" strokeLinejoin="round" />
        <path d="M24 3L42.2 13.5V18C33 20.5 15 20.5 5.8 18V13.5Z" fill="#fff" fillOpacity=".22" />
      </>
    case 'Diamond': return <>
        <path d="M14 9H34L43 19L24 44L5 19Z" fill={`url(#${id}-m)`} stroke="#3B3FCF" strokeWidth="1.5" strokeLinejoin="round" />
        <path d="M5 19H43M14 9L19 19L24 44M34 9L29 19L24 44M19 19L24 9L29 19" fill="none" stroke="#FFFFFF" strokeOpacity=".7" strokeWidth="1.1" strokeLinejoin="round" />
        <path d="M14 9H34L39 14.5H9Z" fill="#fff" fillOpacity=".22" />
      </>
    case 'Master': return <>
        <path d="M24 4A20 20 0 1 1 23.99 4Z" fill="#7A1630" stroke="#4E0C1E" strokeWidth="1.5" />
        <path d="M11 31L9 15.5L17.5 22L24 11L30.5 22L39 15.5L37 31Z" fill={`url(#${id}-m)`} stroke="#A7650A" strokeWidth="1.5" strokeLinejoin="round" />
        <path d="M11 33H37V37.5H11Z" fill={`url(#${id}-m)`} stroke="#A7650A" strokeWidth="1.2" strokeLinejoin="round" />
        <path d="M24 24.5m-2.6 0a2.6 2.6 0 1 0 5.2 0a2.6 2.6 0 1 0 -5.2 0" fill="#E8466A" stroke="#7A1630" strokeWidth=".8" />
      </>
    default: return null
  }
}
