import React from 'react'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { LandingPage } from '../features/landing/pages/LandingPage'
import * as AuthProviderModule from '../app/providers/AuthProvider'

vi.mock('../app/providers/AuthProvider', async () => {
  const actual = await vi.importActual('../app/providers/AuthProvider')
  return {
    ...actual,
    useAuth: vi.fn(),
  }
})

describe('LandingPage', () => {
  beforeEach(() => {
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
  })

  it('renders institutional title, value proposition, and ethical disclaimer', () => {
    render(
      <MemoryRouter>
        <LandingPage />
      </MemoryRouter>
    )

    // Brand and headline
    expect(screen.getAllByText('ResumeMatcher').length).toBeGreaterThanOrEqual(1)
    expect(screen.getByText(/Descubra o quanto seu currículo/i)).toBeInTheDocument()

    // 3-step 'Como funciona'
    expect(screen.getByText(/Como funciona em 3 passos simples/i)).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: /Envie seu currículo/i })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: /Cole a vaga desejada/i })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: /Veja seu diagnóstico/i })).toBeInTheDocument()

    // Illustrative badge
    expect(screen.getByText(/Demonstração Ilustrativa/i)).toBeInTheDocument()

    // CTA buttons
    const ctas = screen.getAllByRole('button', { name: /Analisar meu currículo/i })
    expect(ctas.length).toBeGreaterThanOrEqual(1)
  })

  it('renders ethical architecture and data privacy notices in footer', () => {
    render(
      <MemoryRouter>
        <LandingPage />
      </MemoryRouter>
    )

    expect(screen.getByText(/Não inventamos qualificações/i)).toBeInTheDocument()
    expect(screen.getByText(/política de 30 dias/i)).toBeInTheDocument()
  })
})
