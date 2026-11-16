import type { BadgeTone } from '../../components/common/StatusBadge'
import type { DocumentStatus } from './api'

/** Badge tone per status; the label is `documents.status.<status>` in the locale files. */
export const documentStatus: Record<DocumentStatus, { tone: BadgeTone }> = {
  Pending: { tone: 'neutral' },
  Processing: { tone: 'info' },
  Completed: { tone: 'success' },
  Failed: { tone: 'danger' },
}
