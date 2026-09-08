import React, { createContext, useContext, useEffect, useState, type ReactNode } from 'react'
import { onAuthStateChanged, type User } from 'firebase/auth'
import {
  firebaseAuth,
  loginErrorMessage,
  loginWithGoogle,
  logout,
  verifyApiSession,
  isFirebaseConfigured,
} from '../../features/auth/services/authService'

export interface AuthContextValue {
  user: User | null
  checking: boolean
  authorized: boolean
  authError: string
  busy: boolean
  isConfigured: boolean
  signIn: () => Promise<void>
  signOut: () => Promise<void>
  retrySession: () => void
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null)
  const [checking, setChecking] = useState<boolean>(Boolean(isFirebaseConfigured))
  const [authorized, setAuthorized] = useState<boolean>(false)
  const [busy, setBusy] = useState<boolean>(false)
  const [authError, setAuthError] = useState<string>('')
  const [attempt, setAttempt] = useState<number>(0)

  useEffect(() => {
    if (!firebaseAuth) {
      setChecking(false)
      return
    }

    let version = 0
    const unsubscribe = onAuthStateChanged(firebaseAuth, async (currentUser) => {
      const currentVersion = ++version
      setUser(currentUser)
      setAuthorized(false)
      setAuthError('')
      setChecking(Boolean(currentUser))

      if (!currentUser) {
        setChecking(false)
        return
      }

      try {
        await verifyApiSession()
        if (currentVersion === version) {
          setAuthorized(true)
        }
      } catch (err) {
        if (currentVersion === version) {
          setAuthError(err instanceof Error ? err.message : 'Não foi possível verificar o acesso.')
        }
      } finally {
        if (currentVersion === version) {
          setChecking(false)
        }
      }
    })

    return () => {
      version++
      unsubscribe()
    }
  }, [attempt])

  async function signIn() {
    setBusy(true)
    setAuthError('')
    try {
      await loginWithGoogle()
    } catch (err) {
      setAuthError(loginErrorMessage(err))
    } finally {
      setBusy(false)
    }
  }

  async function signOut() {
    setBusy(true)
    setAuthError('')
    try {
      await logout()
      setUser(null)
      setAuthorized(false)
    } catch {
      setAuthError('Não foi possível encerrar a sessão. Tente novamente.')
    } finally {
      setBusy(false)
    }
  }

  function retrySession() {
    setAttempt((v) => v + 1)
  }

  return (
    <AuthContext.Provider
      value={{
        user,
        checking,
        authorized,
        authError,
        busy,
        isConfigured: isFirebaseConfigured,
        signIn,
        signOut,
        retrySession,
      }}
    >
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext)
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider')
  }
  return context
}
