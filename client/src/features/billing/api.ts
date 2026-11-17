import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../lib/api-client'
import { queryKeys } from '../../lib/query-keys'
import type { PagedResult } from '../documents/api'
import type { Usage } from '../usage/api'

export type PaymentChannel = 'WhatsApp' | 'Instagram' | 'Other'
export type PaymentStatus = 'Pending' | 'Approved' | 'Rejected' | 'Cancelled'
export type PlanChangeKind = 'New' | 'Extend' | 'Upgrade' | 'Downgrade'
export type BulkAction = 'AssignPlan' | 'Extend' | 'CancelPlan' | 'SetLimits' | 'ClearLimits' | 'SetUnlimited'
export type UserStatusFilter = '' | 'active' | 'expiring' | 'expired' | 'free' | 'pending' | 'custom'

export interface PlanLimits { monthlyUploads: number | null; monthlyExams: number | null; monthlyBattles: number | null; monthlyDimTests: number | null; monthlyGuides: number | null }
export interface Plan extends PlanLimits {
  id: string; name: string; description: string | null; price: number; durationDays: number
  isFree: boolean; isActive: boolean; isFeatured: boolean; sortOrder: number
}
export interface CurrentPlan { planId: string | null; name: string; isFree: boolean; endsAt: string | null; next: { planId: string; name: string; startsAt: string; endsAt: string } | null }
export interface PaymentRequest {
  id: string; code: string; planId: string; planName: string; periods: number; days: number; amount: number
  channel: PaymentChannel; status: PaymentStatus; adminNote: string | null; createdAt: string; reviewedAt: string | null
  message: string; whatsAppUrl: string | null
}
export interface Billing {
  enabled: boolean; current: CurrentPlan; plans: Plan[]; whatsAppUrl: string | null; instagramUrl: string | null
  instructions: string | null; pending: PaymentRequest | null
}
export interface SubscriptionItem {
  id: string; planId: string; planName: string; startsAt: string; endsAt: string; source: 'Payment' | 'Manual'
  note: string | null; cancelledAt: string | null; createdAt: string
}
export interface BillingHistory { subscriptions: SubscriptionItem[]; payments: PaymentRequest[] }

export interface CustomLimits extends PlanLimits { unlimited: boolean }
export interface AdminBillingUser {
  id: string; nickname: string; email: string | null; isAdmin: boolean; plan: CurrentPlan; usage: Usage
  custom: CustomLimits | null; hasPending: boolean
}
export interface AdminPayment { userId: string; nickname: string; email: string | null; request: PaymentRequest; reviewedBy: string | null }
export interface PlanChangePreview { kind: PlanChangeKind; currentPlanName: string | null; startsAt: string; endsAt: string; bonusDays: number }
export interface AdminPlan { plan: Plan; activeUsers: number }
export interface BillingSettings { enabled: boolean; whatsAppNumber: string | null; instagramUsername: string | null; paymentInstructions: string | null; updatedAt: string }
export interface BulkResult { updated: number; skipped: number; items: { userId: string; updated: boolean; reason: string | null }[] }
export interface BulkInput extends Partial<PlanLimits> {
  userIds: string[]; action: BulkAction; planId?: string; periods?: number; days?: number; note?: string
}
export type PlanInput = Omit<Plan, 'id' | 'isFree'>

// ---- User ----

export function useBilling() {
  return useQuery({ queryKey: queryKeys.billing.mine, queryFn: () => api.get<Billing>('/billing') })
}
/** Packages for signed-out visitors (landing page); empty while sales are switched off. */
export function usePublicPlans() {
  return useQuery({ queryKey: queryKeys.billing.public, queryFn: () => api.get<Plan[]>('/billing/plans'), staleTime: 10 * 60_000 })
}
export function useBillingHistory() {
  return useQuery({ queryKey: queryKeys.billing.history, queryFn: () => api.get<BillingHistory>('/billing/history') })
}
export function useCreatePaymentRequest() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (input: { planId: string; periods: number; channel: PaymentChannel }) => api.post<PaymentRequest>('/billing/requests', input),
    onSettled: () => client.invalidateQueries({ queryKey: queryKeys.billing.all }),
  })
}
export function useCancelPaymentRequest() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.delete(`/billing/requests/${id}`),
    onSettled: () => client.invalidateQueries({ queryKey: queryKeys.billing.all }),
  })
}

// ---- Admin ----

/** Billing changes move limits too, so usage views are refreshed with them. */
function useInvalidateAll() {
  const client = useQueryClient()
  return () => Promise.all([
    client.invalidateQueries({ queryKey: queryKeys.billing.all }),
    client.invalidateQueries({ queryKey: queryKeys.usage.all }),
  ])
}

export function useAdminBillingUsers(q: string, planId: string, status: UserStatusFilter, page: number) {
  const params = new URLSearchParams({ q, status, page: String(page) })
  if (planId) params.set('planId', planId)
  return useQuery({
    queryKey: queryKeys.billing.adminUsers(q, planId, status, page),
    queryFn: () => api.get<PagedResult<AdminBillingUser>>(`/admin/billing/users?${params}`),
    placeholderData: keepPreviousData,
  })
}
export function useAdminBillingUser(id: string) {
  return useQuery({
    queryKey: queryKeys.billing.adminUser(id),
    queryFn: () => api.get<{ user: AdminBillingUser; history: BillingHistory }>(`/admin/billing/users/${id}`),
    enabled: !!id,
  })
}
export function useBulkBilling() {
  const invalidate = useInvalidateAll()
  return useMutation({ mutationFn: (input: BulkInput) => api.post<BulkResult>('/admin/billing/users/bulk', input), onSuccess: invalidate })
}
export function useAdminPayments(status: string, q: string, page: number) {
  const params = new URLSearchParams({ status, q, page: String(page) })
  return useQuery({
    queryKey: queryKeys.billing.adminRequests(status, q, page),
    queryFn: () => api.get<PagedResult<AdminPayment>>(`/admin/billing/requests?${params}`),
    placeholderData: keepPreviousData,
  })
}
export function usePendingPaymentCount() {
  const query = useAdminPayments('Pending', '', 1)
  return query.data?.totalCount ?? 0
}
export function usePaymentPreview(id: string, periods: number, bonusDays: number | null) {
  const params = new URLSearchParams({ periods: String(periods) })
  if (bonusDays !== null) params.set('bonusDays', String(bonusDays))
  return useQuery({
    queryKey: queryKeys.billing.preview(id, periods, bonusDays),
    queryFn: () => api.get<PlanChangePreview>(`/admin/billing/requests/${id}/preview?${params}`),
    placeholderData: keepPreviousData,
  })
}
export function useApprovePayment() {
  const invalidate = useInvalidateAll()
  return useMutation({
    mutationFn: ({ id, ...body }: { id: string; periods?: number; bonusDays?: number | null; note?: string }) =>
      api.post<PlanChangePreview>(`/admin/billing/requests/${id}/approve`, body),
    onSuccess: invalidate,
  })
}
export function useRejectPayment() {
  const invalidate = useInvalidateAll()
  return useMutation({
    mutationFn: ({ id, note }: { id: string; note?: string }) => api.post(`/admin/billing/requests/${id}/reject`, { note }),
    onSuccess: invalidate,
  })
}
export function useAdminPlans() {
  return useQuery({ queryKey: queryKeys.billing.plans, queryFn: () => api.get<AdminPlan[]>('/admin/billing/plans') })
}
export function useSavePlan() {
  const invalidate = useInvalidateAll()
  return useMutation({
    mutationFn: ({ id, plan }: { id: string | null; plan: PlanInput }) =>
      id ? api.put<Plan>(`/admin/billing/plans/${id}`, plan) : api.post<Plan>('/admin/billing/plans', plan),
    onSuccess: invalidate,
  })
}
export function useBillingSettings() {
  return useQuery({ queryKey: queryKeys.billing.settings, queryFn: () => api.get<BillingSettings>('/admin/billing/settings') })
}
export function useSaveBillingSettings() {
  const invalidate = useInvalidateAll()
  return useMutation({
    mutationFn: (input: Omit<BillingSettings, 'updatedAt'>) => api.put<BillingSettings>('/admin/billing/settings', input),
    onSuccess: invalidate,
  })
}
