import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { useQueryClient } from '@tanstack/react-query'
import { useEffect } from 'react'
import { getToken } from '../../lib/auth-token'
import { queryKeys } from '../../lib/query-keys'

/** One authenticated presence connection across the app, independent of battle-room membership. */
export function SocialConnection() {
  const client = useQueryClient()
  useEffect(() => {
    let disposed = false
    let retry: ReturnType<typeof setTimeout> | undefined
    const connection = new HubConnectionBuilder().withUrl('/hubs/social', { accessTokenFactory: () => getToken() ?? '' })
      .withAutomaticReconnect().configureLogging(LogLevel.None).build()
    const refresh = () => { if (!disposed) void client.invalidateQueries({ queryKey: queryKeys.social.all }) }
    connection.on('SocialChanged', refresh)
    connection.onreconnected(refresh)
    const scheduleRetry = () => {
      clearTimeout(retry)
      if (!disposed) retry = setTimeout(() => { void start() }, 5000)
    }
    async function start() {
      if (disposed || !getToken()) return
      try { await connection.start(); if (disposed) await connection.stop(); else refresh() }
      catch { scheduleRetry() }
    }
    connection.onclose(scheduleRetry)
    void start()
    return () => { disposed = true; clearTimeout(retry); void connection.stop() }
  }, [client])
  return null
}
