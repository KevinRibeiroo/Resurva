import React from 'react'
import { Navigate, useLocation, Outlet } from 'react-router-dom'
import { useAuth } from '../../../app/providers/AuthProvider'
import { Spinner } from '../../../shared/ui/Spinner/Spinner'
import styles from './AuthProtected.module.css'

export function AuthProtected({ children }: { children?: React.ReactNode }) {
  const { user, checking, authorized, authError } = useAuth()
  const location = useLocation()

  if (checking) {
    return (
      <div className={styles.loadingContainer}>
        <Spinner size="lg" label="Verificando acesso seguro ao Resurva…" />
      </div>
    )
  }

  if (!user || !authorized) {
    return <Navigate to="/login" state={{ from: location, error: authError }} replace />
  }

  return children ? <>{children}</> : <Outlet />
}

