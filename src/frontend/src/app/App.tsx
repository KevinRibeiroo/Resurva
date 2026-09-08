import React from 'react'
import { AuthProvider } from './providers/AuthProvider'
import { AppRouter } from './AppRouter'

export function App() {
  return (
    <AuthProvider>
      <AppRouter />
    </AuthProvider>
  )
}
