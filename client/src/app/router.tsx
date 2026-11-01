import { createBrowserRouter } from 'react-router'
import { ResetPasswordPage } from '../features/auth/ResetPasswordPage'
import { BattleHomePage } from '../features/battle/BattleHomePage'
import { BattleRoomPage } from '../features/battle/BattleRoomPage'
import { DashboardPage } from '../features/dashboard/DashboardPage'
import { UploadPage } from '../features/documents/UploadPage'
import { ExamHistoryPage } from '../features/exams/ExamHistoryPage'
import { ExamResultPage } from '../features/exams/ExamResultPage'
import { ExamSessionPage } from '../features/exams/ExamSessionPage'
import { ExamSetupPage } from '../features/exams/ExamSetupPage'
import { QuestionBankPage } from '../features/questions/QuestionBankPage'
import { AppLayout } from './AppLayout'
import { NotFoundPage } from './NotFoundPage'
import { RequireAuth } from './RequireAuth'

export const router = createBrowserRouter([
  {
    element: (
      <RequireAuth>
        <AppLayout />
      </RequireAuth>
    ),
    children: [
      { index: true, element: <DashboardPage /> },
      { path: 'upload', element: <UploadPage /> },
      { path: 'questions', element: <QuestionBankPage /> },
      { path: 'exams/new', element: <ExamSetupPage /> },
      { path: 'exams', element: <ExamHistoryPage /> },
      { path: 'exams/:id/result', element: <ExamResultPage /> },
      { path: 'battle', element: <BattleHomePage /> },
      { path: '*', element: <NotFoundPage /> },
    ],
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
