import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { useAuth } from './authState'

type ProtectedRouteProps = {
  children: ReactNode
  allowIncompleteProfile?: boolean
  requiredRole?: string
}

export function ProtectedRoute({
  children,
  allowIncompleteProfile = false,
  requiredRole,
}: ProtectedRouteProps) {
  const { user, isLoading } = useAuth()
  const location = useLocation()

  if (isLoading) {
    return (
      <div className="auth-loading" role="status">
        <span />
        <p>Đang khôi phục phiên đăng nhập...</p>
      </div>
    )
  }

  if (!user) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
  }

  if (!allowIncompleteProfile && !user.profileCompleted) {
    return <Navigate to="/onboarding" replace />
  }

  if (requiredRole && user.role !== requiredRole) {
    return <Navigate to="/catalog" replace />
  }

  return children
}
