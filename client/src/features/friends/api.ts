import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../lib/api-client'
import { queryKeys } from '../../lib/query-keys'
import { useCurrentUser } from '../auth/api'

export interface Friend { id: string; displayName: string; isOnline: boolean; avatarUrl: string | null }
export interface SearchUser { id: string; displayName: string; relationship: 'Friend' | 'Incoming' | 'Outgoing' | 'None'; avatarUrl: string | null }
export interface Friends { friends: Friend[]; incoming: Friend[]; outgoing: Friend[]; blocked: Friend[] }
export interface Lobby { code: string; hostUserId: string; hostName: string; documentName: string; questionCount: number }
export interface RoomFriend extends Friend { isInRoom: boolean; invitationSent: boolean }
export interface SocialNotification {
  id: string; actorId: string; actorName: string; kind: 'FriendRequest' | 'FriendAccepted' | 'BattleInvite' | 'GroupAdded'
  status: 'Pending' | 'Accepted' | 'Rejected' | 'Expired' | 'Unavailable'
  roomCode: string | null; hostName: string | null; documentName: string | null; questionCount: number | null
  createdAt: string; expiresAt: string | null; isRead: boolean
}
export interface Notifications { unreadCount: number; items: SocialNotification[] }
export type FriendAction = 'request' | 'accept' | 'reject' | 'remove' | 'block' | 'unblock'

export function useFriends() {
  const { data: user } = useCurrentUser()
  return useQuery({ queryKey: queryKeys.social.friends(user?.id ?? ''), enabled: !!user,
    queryFn: () => api.get<Friends>('/friends'), refetchInterval: 30_000 })
}

export function useUserSearch(term: string) {
  const { data: user } = useCurrentUser()
  return useQuery({ queryKey: queryKeys.social.search(user?.id ?? '', term), enabled: !!user && term.length >= 2,
    queryFn: () => api.get<SearchUser[]>(`/friends/search?q=${encodeURIComponent(term)}`) })
}

export function useNotifications() {
  const { data: user } = useCurrentUser()
  return useQuery({ queryKey: queryKeys.social.notifications(user?.id ?? ''), enabled: !!user,
    queryFn: () => api.get<Notifications>('/notifications'), refetchInterval: 15_000 })
}

export function useLobbies() {
  const { data: user } = useCurrentUser()
  return useQuery({ queryKey: queryKeys.social.lobbies(user?.id ?? ''), enabled: !!user,
    queryFn: () => api.get<Lobby[]>('/friends/lobbies'), refetchInterval: 10_000 })
}

export function useFriendAction() {
  const client = useQueryClient()
  return useMutation({ mutationFn: ({ id, action }: { id: string; action: FriendAction }) => api.post<void>(`/friends/${id}/${action}`),
    onSuccess: () => client.invalidateQueries({ queryKey: queryKeys.social.all }) })
}

export function useInviteFriend(onlineOnly = false) {
  const client = useQueryClient()
  return useMutation({ mutationFn: ({ id, roomCode }: { id: string; roomCode: string }) => api.post<void>(`/friends/${id}/${onlineOnly ? 'room-invite' : 'invite'}`, { roomCode }),
    onSuccess: () => client.invalidateQueries({ queryKey: queryKeys.social.all }) })
}

export function useRoomFriends(code: string, term: string) {
  const { data: user } = useCurrentUser()
  return useQuery({ queryKey: queryKeys.social.roomFriends(code, user?.id ?? '', term), enabled: !!user,
    queryFn: () => api.get<RoomFriend[]>(`/friends/room/${code}?q=${encodeURIComponent(term)}`), refetchInterval: 10_000 })
}

export function useNotificationAction() {
  const client = useQueryClient()
  return useMutation({ mutationFn: ({ id, action }: { id?: string; action: 'read' | 'accept' | 'reject' }) =>
    api.post<{ code: string } | undefined>(`/notifications/${id ? `${id}/` : ''}${action}`),
    onSettled: () => client.invalidateQueries({ queryKey: queryKeys.social.all }) })
}
