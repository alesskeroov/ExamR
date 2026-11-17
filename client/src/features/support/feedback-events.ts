export type FeedbackKind = 'problem' | 'idea'

const eventName = 'examr:open-feedback'

export interface OpenFeedbackDetail { kind?: FeedbackKind; documentName?: string }

/** Opens the floating feedback widget from any page (e.g. the guide's own "write to us" button). */
export function openFeedback(detail: OpenFeedbackDetail = {}) {
  window.dispatchEvent(new CustomEvent<OpenFeedbackDetail>(eventName, { detail }))
}

export function onOpenFeedback(handler: (detail: OpenFeedbackDetail) => void) {
  const listener = (event: Event) => handler((event as CustomEvent<OpenFeedbackDetail>).detail ?? {})
  window.addEventListener(eventName, listener)
  return () => window.removeEventListener(eventName, listener)
}
