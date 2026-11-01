import type { BadgeTone } from '../../components/common/StatusBadge'
import type { DocumentStatus } from './api'

export const documentStatus: Record<DocumentStatus, { tone: BadgeTone; label: string }> = {
  Pending: { tone: 'neutral', label: 'Növbədə' },
  Processing: { tone: 'info', label: 'Emal olunur' },
  Completed: { tone: 'success', label: 'Hazırdır' },
  Failed: { tone: 'danger', label: 'Xəta' },
}
