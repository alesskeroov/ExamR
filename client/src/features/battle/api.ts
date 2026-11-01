import { HubConnectionBuilder, HubConnectionState, LogLevel, type HubConnection } from '@microsoft/signalr'
import { keepPreviousData, useMutation, useQuery } from '@tanstack/react-query'
import { useCallback, useEffect, useRef, useState } from 'react'
import { api } from '../../lib/api-client'
import { getToken } from '../../lib/auth-token'
import { queryKeys } from '../../lib/query-keys'
import type { PagedResult } from '../documents/api'
import type { ExamOption, ExamSource } from '../exams/api'

export type BattleStatus = 'Lobby' | 'Question' | 'Reveal' | 'Finished' | 'Closed'

export interface BattleSettings {
  documentId: string
  documentName: string
  rangeFrom: number
  rangeTo: number
  questionCount: number
  secondsPerQuestion: number
}

export interface BattleParticipant {
  userId: string
  displayName: string
  isHost: boolean
  isConnected: boolean
  isMuted: boolean
  score: number
  correctCount: number
  rank: number
  lastPoints: number | null
  lastCorrect: boolean | null
  hasAnswered: boolean
}

export interface BattleQuestion {
  index: number
  total: number
  text: string
  options: ExamOption[]
  endsAt: string
  secondsPerQuestion: number
}

export interface BattleState {
  code: string
  hostUserId: string
  status: BattleStatus
  settings: BattleSettings
  participants: BattleParticipant[]
  question: BattleQuestion | null
  /** Only present after the question closed. */
  reveal: { index: number; correctOptionId: string | null } | null
  answeredCount: number
  serverNow: string
  myAnswerOptionId: string | null
  recentMessages: ChatMessage[] | null
}

export interface ChatMessage {
  id: string
  userId: string
  displayName: string
  text: string
  sentAt: string
}

export interface CreateBattleRequest {
  documentId: string
  rangeFrom: number
  rangeTo: number
  questionCount: number
  secondsPerQuestion: number
}

export interface BattleHistoryItem {
  battleId: string
  roomCode: string
  playedAt: string
  source: ExamSource
  questionCount: number
  participantCount: number
  rank: number
  score: number
  correctCount: number
}

export const MAX_BATTLE_QUESTIONS = 50
export const MAX_MESSAGE_LENGTH = 300

export function useCreateBattle() {
  return useMutation({ mutationFn: (request: CreateBattleRequest) => api.post<{ code: string }>('/battles', request) })
}

export function useBattleHistory(page: number) {
  return useQuery({
    queryKey: queryKeys.battles.history(page),
    queryFn: () => api.get<PagedResult<BattleHistoryItem>>(`/battles/history?page=${page}&pageSize=20`),
    placeholderData: keepPreviousData,
  })
}

/** Server errors arrive as "... HubException: <message>"; keep only the message. */
export function hubErrorMessage(error: unknown) {
  const message = error instanceof Error ? error.message : String(error)
  return message.includes('HubException: ') ? message.split('HubException: ').pop()! : 'Serverlə əlaqə kəsildi.'
}

export type ConnectionStatus = 'connecting' | 'connected' | 'reconnecting' | 'disconnected'

/**
 * One SignalR connection per open room page. Joins on connect and re-joins after an automatic
 * reconnect, so the room state (score, current question, own answer, recent chat) is restored.
 */
export function useBattleRoom(code: string) {
  const [state, setRawState] = useState<BattleState | null>(null)
  // Server clock minus client clock, measured when each state arrives (for the question timer).
  const [serverOffset, setServerOffset] = useState(0)
  const setState = useCallback((next: BattleState) => {
    setRawState(next)
    setServerOffset(Date.parse(next.serverNow) - Date.now())
  }, [])
  const [messages, setMessages] = useState<ChatMessage[]>([])
  const [myAnswer, setMyAnswer] = useState<{ index: number; optionId: string } | null>(null)
  const [status, setStatus] = useState<ConnectionStatus>('connecting')
  const [fatal, setFatal] = useState<string | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)
  const connectionRef = useRef<HubConnection | null>(null)

  useEffect(() => {
    let disposed = false
    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/battle', { accessTokenFactory: () => getToken() ?? '' })
      .withAutomaticReconnect([0, 1000, 2000, 5000, 10000, 10000, 15000, 30000])
      .configureLogging(LogLevel.Warning)
      .build()
    connectionRef.current = connection

    // Handlers must not return a value (SignalR would treat it as a client result).
    connection.on('RoomUpdated', (next: BattleState) => {
      setState(next)
    })
    connection.on('ChatMessage', (message: ChatMessage) => {
      setMessages((current) => [...current.slice(-99), message])
    })
    connection.on('Kicked', (message: string) => {
      setFatal(message)
      void connection.stop()
    })
    connection.on('RoomClosed', (message: string) => {
      setFatal(message)
    })

    const join = async () => {
      const joined = await connection.invoke<BattleState>('JoinRoom', code)
      if (disposed) return
      setState(joined)
      setMessages(joined.recentMessages ?? [])
      setMyAnswer(joined.question && joined.myAnswerOptionId ? { index: joined.question.index, optionId: joined.myAnswerOptionId } : null)
    }

    connection.onreconnecting(() => {
      setStatus('reconnecting')
    })
    connection.onreconnected(() => {
      setStatus('connected')
      join().catch((error) => setFatal(hubErrorMessage(error)))
    })
    connection.onclose(() => {
      if (!disposed) setStatus('disconnected')
    })

    connection
      .start()
      .then(() => {
        if (disposed) return
        setStatus('connected')
        return join()
      })
      .catch((error) => {
        if (!disposed) setFatal(hubErrorMessage(error))
      })

    return () => {
      disposed = true
      void connection.stop()
    }
  }, [code, setState])

  const invoke = useCallback(async (method: string, ...args: unknown[]) => {
    const connection = connectionRef.current
    if (!connection || connection.state !== HubConnectionState.Connected) {
      setActionError('Bağlantı bərpa olunur, bir az gözləyin.')
      return false
    }

    try {
      setActionError(null)
      await connection.invoke(method, ...args)
      return true
    } catch (error) {
      setActionError(hubErrorMessage(error))
      return false
    }
  }, [])

  const answer = useCallback(
    async (index: number, optionId: string) => {
      setMyAnswer({ index, optionId })
      if (!(await invoke('SubmitAnswer', code, index, optionId))) setMyAnswer(null)
    },
    [code, invoke],
  )

  return {
    state,
    messages,
    status,
    fatal,
    actionError,
    clearActionError: () => setActionError(null),
    myAnswerOptionId: myAnswer && state?.question && myAnswer.index === state.question.index ? myAnswer.optionId : null,
    serverOffset,
    start: () => invoke('StartGame', code),
    advance: () => invoke('Advance', code),
    answer,
    send: (text: string) => invoke('SendMessage', code, text),
    setMuted: (userId: string, muted: boolean) => invoke('SetMuted', code, userId, muted),
    kick: (userId: string) => invoke('Kick', code, userId),
    leave: () => invoke('LeaveRoom', code),
  }
}
