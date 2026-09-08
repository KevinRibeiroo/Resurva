import React from 'react'
import { render, screen, fireEvent } from '@testing-library/react'
import { MemoryRouter, Routes, Route } from 'react-router-dom'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { LoginPage } from '../features/auth/pages/LoginPage'
import { AuthProtected } from '../features/auth/components/AuthProtected'
import * as AuthProviderModule from '../app/providers/AuthProvider'

vi.mock('../app/providers/AuthProvider', async () => {
  const actual = await vi.importActual('../app/providers/AuthProvider')
  return {
    ...actual,
    useAuth: vi.fn(),
  }
})

describe('Authentication flows', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('renders Google sign-in button when user is unauthenticated', () => {
    vi.mocked(AuthProviderModule.useAuth).mockReturnValue({
      user: null,
      checking: false,
      authorized: false,
      authError: '',
      busy: false,
      isConfigured: true,
      signIn: vi.fn(),
      signOut: vi.fn(),
      retrySession: vi.fn(),
    })

    render(
      <MemoryRouter initialEntries={['/login']}>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
        </Routes>
      </MemoryRouter>
    )

    expect(screen.getByRole('button', { name: /Entrar com Google/i })).toBeInTheDocument()
    expect(screen.getByText(/Acesso Restrito/i)).toBeInTheDocument()
  })

  it('renders unauthorized state with user email when user is logged in but not in backend whitelist', () => {
    const mockRetry = vi.fn()
    const mockSignOut = vi.fn()

    vi.mocked(AuthProviderModule.useAuth).mockReturnValue({
      user: { email: 'unauthorized@example.com', displayName: 'Test User', photoURL: null } as any,
      checking: false,
      authorized: false,
      authError: 'Acesso restrito ao e-mail aprovado.',
      busy: false,
      isConfigured: true,
      signIn: vi.fn(),
      signOut: mockSignOut,
      retrySession: mockRetry,
    })

    render(
      <MemoryRouter initialEntries={['/login']}>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
        </Routes>
      </MemoryRouter>
    )

    expect(screen.getByText(/Conta não autorizada/i)).toBeInTheDocument()
    expect(screen.getByText('unauthorized@example.com')).toBeInTheDocument()
    expect(screen.getByText(/Acesso restrito ao e-mail aprovado/i)).toBeInTheDocument()

    // Test retry button
    const retryBtn = screen.getByRole('button', { name: /Verificar novamente/i })
    fireEvent.click(retryBtn)
    expect(mockRetry).toHaveBeenCalled()

    // Test switch account button
    const signOutBtn = screen.getByRole('button', { name: /Trocar de conta Google/i })
    fireEvent.click(signOutBtn)
    expect(mockSignOut).toHaveBeenCalled()
  })

  it('redirects unauthenticated user from protected route to /login', () => {
    vi.mocked(AuthProviderModule.useAuth).mockReturnValue({
      user: null,
      checking: false,
      authorized: false,
      authError: '',
      busy: false,
      isConfigured: true,
      signIn: vi.fn(),
      signOut: vi.fn(),
      retrySession: vi.fn(),
    })

    render(
      <MemoryRouter initialEntries={['/app/analises/nova']}>
        <Routes>
          <Route path="/login" element={<div>Tela de Login</div>} />
          <Route
            path="/app/analises/nova"
            element={
              <AuthProtected>
                <div>Conteúdo Protegido</div>
              </AuthProtected>
            }
          />
        </Routes>
      </MemoryRouter>
    )

    expect(screen.getByText('Tela de Login')).toBeInTheDocument()
    expect(screen.queryByText('Conteúdo Protegido')).not.toBeInTheDocument()
  })

  it('allows access to protected route when user is authorized', () => {
    vi.mocked(AuthProviderModule.useAuth).mockReturnValue({
      user: { email: 'authorized@example.com' } as any,
      checking: false,
      authorized: true,
      authError: '',
      busy: false,
      isConfigured: true,
      signIn: vi.fn(),
      signOut: vi.fn(),
      retrySession: vi.fn(),
    })

    render(
      <MemoryRouter initialEntries={['/app/analises/nova']}>
        <Routes>
          <Route
            path="/app/analises/nova"
            element={
              <AuthProtected>
                <div>Conteúdo Protegido</div>
              </AuthProtected>
            }
          />
        </Routes>
      </MemoryRouter>
    )

    expect(screen.getByText('Conteúdo Protegido')).toBeInTheDocument()
  })
})
