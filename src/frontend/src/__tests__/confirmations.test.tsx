import React from 'react'
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { MemoryRouter, Routes, Route } from 'react-router-dom'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { OptimizationConfirmationsPage } from '../features/optimization/pages/OptimizationConfirmationsPage'
import * as optimizationService from '../features/optimization/services/optimizationService'

const mockNavigate = vi.fn()
vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual('react-router-dom')
  return {
    ...actual,
    useNavigate: () => mockNavigate,
  }
})

vi.mock('../features/optimization/services/optimizationService', () => ({
  createOptimizationPlan: vi.fn(),
  getOptimizationPlan: vi.fn(),
  applyOptimization: vi.fn(),
  exportAdaptedResume: vi.fn(),
}))

const mockPlanWithNeedsConfirmation = {
  id: 'plan-xyz',
  analysisId: 'analysis-123',
  resumeId: 'resume-123',
  version: 1,
  status: 'Draft',
  originalText: 'Curriculo original de teste',
  adaptedText: null,
  suggestions: [
    {
      id: 'sug-safe-1',
      level: 0, // Safe
      originalText: 'Desenvolvedor backend',
      proposedText: 'Engenheiro de Software Backend Pleno',
      reason: 'Melhora o vocabulário para alinhar com o título da vaga',
      evidence: 'Experiência no cargo',
    },
    {
      id: 'sug-confirm-1',
      level: 1, // NeedsConfirmation
      originalText: null,
      proposedText: 'Experiência prática com orquestração de microsserviços em Docker e Kubernetes',
      reason: 'Vaga valoriza Kubernetes',
      evidence: null,
    },
  ],
  createdAt: '2026-09-08T00:00:00Z',
  updatedAt: '2026-09-08T00:00:00Z',
}

describe('OptimizationConfirmationsPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('starts with NeedsConfirmation items unconfirmed and not pre-selected', async () => {
    vi.mocked(optimizationService.getOptimizationPlan).mockResolvedValue(mockPlanWithNeedsConfirmation as any)

    render(
      <MemoryRouter initialEntries={['/app/adaptacoes/plan-xyz/confirmacoes']}>
        <Routes>
          <Route
            path="/app/adaptacoes/:optimizationId/confirmacoes"
            element={<OptimizationConfirmationsPage />}
          />
        </Routes>
      </MemoryRouter>
    )

    await waitFor(() => {
      expect(screen.getByText('Confirmação de Informações')).toBeInTheDocument()
    })

    // Item text
    expect(
      screen.getByText(/Experiência prática com orquestração de microsserviços em Docker e Kubernetes/i)
    ).toBeInTheDocument()

    // Radio options should not be checked
    const radioNone = screen.getByLabelText(/Não possuo experiência ou conhecimento/i)
    expect(radioNone).not.toBeChecked()

    // Proceed button
    expect(screen.getByRole('button', { name: /Confirmar e Gerar Versão Adaptada/i })).toBeInTheDocument()
  })

  it('invalidates confirmation when declaration text is edited', async () => {
    vi.mocked(optimizationService.getOptimizationPlan).mockResolvedValue(mockPlanWithNeedsConfirmation as any)

    render(
      <MemoryRouter initialEntries={['/app/adaptacoes/plan-xyz/confirmacoes']}>
        <Routes>
          <Route
            path="/app/adaptacoes/:optimizationId/confirmacoes"
            element={<OptimizationConfirmationsPage />}
          />
        </Routes>
      </MemoryRouter>
    )

    await waitFor(() => {
      expect(screen.getByLabelText(/Tenho conhecimento \/ estudo/i)).toBeInTheDocument()
    })

    // Select theoretical knowledge
    const theoreticalRadio = screen.getByLabelText(/Tenho conhecimento \/ estudo/i)
    fireEvent.click(theoreticalRadio)

    // Textarea appears for custom declaration
    const textarea = screen.getByLabelText(/Redija o texto exato que será incorporado/i)
    expect(textarea).toBeInTheDocument()

    // Type declaration
    fireEvent.change(textarea, {
      target: { value: 'Estudei Kubernetes em curso e fiz laboratórios práticos.' },
    })

    // Checkbox to confirm
    const checkbox = screen.getByRole('checkbox', {
      name: /Confirmo que a declaração acima é verídica/i,
    })
    expect(checkbox).not.toBeChecked()

    // Check it
    fireEvent.click(checkbox)
    expect(checkbox).toBeChecked()

    // Edit declaration again -> should automatically invalidate (uncheck) confirmation
    fireEvent.change(textarea, {
      target: { value: 'Estudei Kubernetes em curso, fiz laboratórios e criei clusters locais.' },
    })
    expect(checkbox).not.toBeChecked()
  })

  it('skipping confirmations preserves Safe items and rejects unconfirmed items', async () => {
    vi.mocked(optimizationService.getOptimizationPlan).mockResolvedValue(mockPlanWithNeedsConfirmation as any)

    render(
      <MemoryRouter initialEntries={['/app/adaptacoes/plan-xyz/confirmacoes']}>
        <Routes>
          <Route
            path="/app/adaptacoes/:optimizationId/confirmacoes"
            element={<OptimizationConfirmationsPage />}
          />
        </Routes>
      </MemoryRouter>
    )

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /Pular confirmações e manter dados originais/i })).toBeInTheDocument()
    })

    const skipBtn = screen.getByRole('button', { name: /Pular confirmações e manter dados originais/i })
    fireEvent.click(skipBtn)

    expect(mockNavigate).toHaveBeenCalledWith('/app/adaptacoes/plan-xyz', {
      state: {
        plan: mockPlanWithNeedsConfirmation,
        decisions: [
          {
            suggestionId: 'sug-safe-1',
            accepted: true,
            confirmed: false,
          },
          {
            suggestionId: 'sug-confirm-1',
            accepted: false,
            confirmed: false,
          },
        ],
      },
    })
  })
})
