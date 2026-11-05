import { AdminPage } from '../features/admin/AdminPage'
import { GroupDetailPage, GroupsAdminPage, GroupsPage } from '../features/groups/GroupsPage'
import { TutorPage } from '../features/tutor/TutorPage'
import { TutorUnavailablePage } from '../features/tutor/TutorUnavailablePage'
import { tutorEnabled } from '../lib/features'
import { createBrowserRouter, Navigate } from 'react-router'
import { ResetPasswordPage } from '../features/auth/ResetPasswordPage'
import { VerifyEmailPage } from '../features/auth/VerifyEmailPage'
import { BattleHomePage } from '../features/battle/BattleHomePage'
import { BattleRoomPage } from '../features/battle/BattleRoomPage'
import { MyRoomsPage } from '../features/battle/MyRoomsPage'
import { DashboardPage } from '../features/dashboard/DashboardPage'
import { UploadPage } from '../features/documents/UploadPage'
import { ExamHistoryPage } from '../features/exams/ExamHistoryPage'
import { ExamResultPage } from '../features/exams/ExamResultPage'
import { ExamSessionPage } from '../features/exams/ExamSessionPage'
import { ExamSetupPage } from '../features/exams/ExamSetupPage'
import { QuestionBankPage } from '../features/questions/QuestionBankPage'
import { FriendsPage } from '../features/friends/FriendsPage'
import { NotificationsPage } from '../features/friends/NotificationsPage'
import { ProfilePage } from '../features/auth/ProfilePage'
import { AppLayout } from './AppLayout'
import { NotFoundPage } from './NotFoundPage'
import { RequireAuth } from './RequireAuth'
import { StatusPage } from './StatusPage'

export const router = createBrowserRouter([
  {
    element: (
      <RequireAuth>
        <AppLayout />
      </RequireAuth>
    ),
    // Route-level failures (lazy load, render) get the same friendly screen instead of the router's raw error.
    errorElement: <StatusPage code={500} />,
    children: [
      { index: true, element: <DashboardPage /> },
      { path: 'upload', element: <UploadPage /> },
      // Retired features (Learning Assistant, Focus Mode): old links land on the dashboard.
      { path: 'study', element: <Navigate to="/" replace /> },
      { path: 'tutor', element: tutorEnabled ? <TutorPage /> : <TutorUnavailablePage /> },
      { path: 'focus', element: <Navigate to="/" replace /> },
      { path: 'questions', element: <QuestionBankPage /> },
      { path: 'exams/new', element: <ExamSetupPage /> },
      { path: 'exams', element: <ExamHistoryPage /> },
      { path: 'exams/:id/result', element: <ExamResultPage /> },
      { path: 'battle', element: <BattleHomePage /> },
      { path: 'battle/rooms', element: <MyRoomsPage /> },
      { path: 'friends', element: <FriendsPage /> },
      { path: 'groups', element: <GroupsPage /> },
      { path: 'groups/:id', element: <GroupDetailPage /> },
      { path: 'admin', element: <AdminPage /> },
      { path: 'admin/groups', element: <GroupsAdminPage /> },
      { path: 'notifications', element: <NotificationsPage /> },
      { path: 'profile', element: <ProfilePage /> },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
  {
    // Public: the API redirects browser navigations here (?code=404|403|500) instead of returning raw JSON.
    path: 'error',
    element: <StatusPage />,
  },
  {
    // Public: opened from the verification / welcome email.
    path: 'verify-email',
    element: <VerifyEmailPage />,
  },
  {
    // Public: opened from the password-reset email.
    path: 'reset-password',
    element: <ResetPasswordPage />,
  },
  {
    // Live battle room (shareable link /battle/CODE): focused screen with its own header and chat.
    path: 'battle/:code',
    element: (
      <RequireAuth>
        <BattleRoomPage />
      </RequireAuth>
    ),
  },
  {
    // Focused exam screen: no sidebar.
    path: 'exams/:id',
    element: (
      <RequireAuth>
        <ExamSessionPage />
      </RequireAuth>
    ),
  },
])
