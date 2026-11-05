/**
 * AI Tutor is still in development: on in the local dev server, off in production builds
 * unless the build sets VITE_TUTOR_ENABLED=true.
 */
export const tutorEnabled = import.meta.env.DEV || import.meta.env.VITE_TUTOR_ENABLED === 'true'
