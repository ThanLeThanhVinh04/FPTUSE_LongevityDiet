import { lazy, Suspense } from 'react'
import { Navigate, Route, Routes } from 'react-router-dom'
import { ProtectedRoute } from './auth/ProtectedRoute'

const HomePage = lazy(() =>
  import('./pages/HomePage').then((module) => ({ default: module.HomePage })),
)

const LoginPage = lazy(() =>
  import('./pages/auth/LoginPage').then((module) => ({ default: module.LoginPage })),
)

const RegisterPage = lazy(() =>
  import('./pages/auth/RegisterPage').then((module) => ({ default: module.RegisterPage })),
)

const NotificationsPage = lazy(() =>
  import('./pages/tracking/NotificationsPage')
    .then((module) => ({ default: module.NotificationsPage })),
)

const ActivityTrackingPage = lazy(() =>
  import('./pages/tracking/ActivityTrackingPage')
    .then((module) => ({ default: module.ActivityTrackingPage })),
)

const profilePages = () => import('./pages/auth/ProfileEditorPage')

const OnboardingPage = lazy(() =>
  profilePages().then((module) => ({ default: module.OnboardingPage })),
)

const ProfilePage = lazy(() =>
  profilePages().then((module) => ({ default: module.ProfilePage })),
)

function RouteLoading() {
  return (
    <div className="auth-loading" role="status">
      <span />
      <p>Đang tải nội dung...</p>
    </div>
  )
}

function App() {
  return (
    <Suspense fallback={<RouteLoading />}>
      <Routes>
        <Route path="/" element={<HomePage />} />
        <Route path="/login" element={<LoginPage />} />
        <Route path="/register" element={<RegisterPage />} />

        <Route
          path="/onboarding"
          element={
            <ProtectedRoute allowIncompleteProfile>
              <OnboardingPage />
            </ProtectedRoute>
          }
        />

        <Route
          path="/profile"
          element={
            <ProtectedRoute allowIncompleteProfile>
              <ProfilePage />
            </ProtectedRoute>
          }
        />

        <Route
          path="/app"
          element={
            <ProtectedRoute>
              <HomePage />
            </ProtectedRoute>
          }
        />

        <Route
          path="/activity"
          element={
            <ProtectedRoute>
              <ActivityTrackingPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/notifications"
          element={
            <ProtectedRoute>
              <NotificationsPage />
            </ProtectedRoute>
          }
        />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </Suspense>
  )
}

export default App
