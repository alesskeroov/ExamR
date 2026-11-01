import { createBrowserRouter } from 'react-router'
import { DashboardPage } from '../features/dashboard/DashboardPage'
import { UploadPage } from '../features/documents/UploadPage'
import { ExamHistoryPage } from '../features/exams/ExamHistoryPage'
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
      { path: '*', element: <NotFoundPage /> },
    ],
  },
])
