import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { useQueryClient } from '@tanstack/react-query'
import { useEffect, useState, type ReactNode } from 'react'
import { queryKeys } from '../../lib/query-keys'
import { SocialConnected } from './social-connection-state'

/** One authenticated presence connection across the app, independent of battle-room membership. */
export function SocialConnection({ children }: { children: ReactNode }) {
  const client = useQueryClient()
  const [connected, setConnected] = useState(false)
  useEffect(() => {
    let disposed = false
    let retry: ReturnType<typeof setTimeout> | undefined
    const connection = new HubConnectionBuilder().withUrl('/hubs/social')
      .withAutomaticReconnect().configureLogging(LogLevel.None).build()
    const refresh = () => { if (!disposed) void client.invalidateQueries({ queryKey: queryKeys.social.all }) }
    connection.on('SocialChanged', refresh)
    const onConnected = () => {
      if (disposed) return
      setConnected(true)
      refresh()
    }
    connection.onreconnected(onConnected)
    connection.onreconnecting(() => { if (!disposed) setConnected(false) })
    const scheduleRetry = () => {
      clearTimeout(retry)
      if (!disposed) retry = setTimeout(() => { void start() }, 5000)
    }
    async function start() {
      if (disposed) return
      try { await connection.start(); if (disposed) await connection.stop(); else onConnected() }
      catch { scheduleRetry() }
    }
    connection.onclose(() => {
      if (disposed) return
      setConnected(false)
      scheduleRetry()
    })
    void start()
    return () => { disposed = true; clearTimeout(retry); void connection.stop() }
  }, [client])
  return <SocialConnected.Provider value={connected}>{children}</SocialConnected.Provider>
}
