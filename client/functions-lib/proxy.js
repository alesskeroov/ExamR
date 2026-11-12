// Cloudflare Pages Function: forwards /api and /hubs to the Render backend so the browser only talks to the
// Pages domain. Same-origin keeps the HttpOnly SameSite=Strict session cookie, the CSRF header and the Google
// OAuth callback working without CORS. WebSocket upgrades (SignalR) pass through fetch unchanged.
export async function proxy({ request, env }) {
  if (!env.API_ORIGIN) return new Response('API_ORIGIN is not configured', { status: 500 })
  const incoming = new URL(request.url)
  const target = new URL(incoming.pathname + incoming.search, env.API_ORIGIN)
  const forwarded = new Request(target, request)
  // Overwrite (not append) so clients cannot spoof their IP for the API's per-IP rate limits.
  forwarded.headers.set('X-Forwarded-For', request.headers.get('CF-Connecting-IP') ?? '')
  forwarded.headers.set('X-Forwarded-Proto', 'https')
  const response = await fetch(forwarded, { redirect: 'manual' })
  // A sleeping Render free service refuses wake-ups from server-side callers ("hibernate-rate-limited") and only
  // starts after a person opens it in a browser. Tell the client where to send the user.
  if (!(response.headers.get('x-render-routing') ?? '').startsWith('hibernate')) return response
  const marked = new Response(response.body, response)
  marked.headers.set('X-ExamR-Wake-Url', new URL('/api/health/live', env.API_ORIGIN).href)
  return marked
}
