import { useState, type ReactNode } from 'react'
import { VerifiedBadge } from '../../components/common/VerifiedBadge'
import { NameplateArt } from './NameplateArt'
import './nameplates.css'

interface Props {
  effect: string | null
  nickname: string
  avatarUrl?: string | null
  verified?: boolean
  action?: ReactNode
  profileLink?: ReactNode
  /** Short sidebar variant: the art is scaled down instead of cropped. */
  compact?: boolean
}

export function Nameplate({ effect, nickname, avatarUrl, verified, action, profileLink, compact }: Props) {
  const [brokenAvatar, setBrokenAvatar] = useState<string | null>(null)
  return <div className={`nameplate ${effect ?? 'nameplate-plain'}${compact ? ' nameplate-compact' : ''}`}>
    <div className="nameplate-art"><NameplateArt effect={effect} /></div>
    <div className="nameplate-content">
      <span className="nameplate-avatar" aria-hidden>
        {avatarUrl && brokenAvatar !== avatarUrl
          ? <img src={avatarUrl} alt="" onError={() => setBrokenAvatar(avatarUrl)} />
          : nickname.charAt(0).toUpperCase()}
      </span>
      <span className="nameplate-identity">
        {profileLink ?? <span className="nameplate-nickname" title={nickname}>{nickname}</span>}
        {verified && <VerifiedBadge />}
      </span>
      {action && <span className="nameplate-action">{action}</span>}
    </div>
  </div>
}
