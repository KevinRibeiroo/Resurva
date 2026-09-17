import React from 'react'
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { MemoryRouter, Routes, Route } from 'react-router-dom'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { OptimizationAdaptationPage } from '../features/optimization/pages/OptimizationAdaptationPage'
import * as optimizationService from '../features/optimization/services/optimizationService'
import { ApiError } from '../shared/api/httpClient'

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

const mockPlanDraft = {
  id: 'plan-adapt-123',
  analysisId: 'analysis-123',
  resumeId: 'resume-123',
  version: 1,
  status: 'Draft',
  originalText: 'João Silva - Desenvolvedor Backend com 5 anos de experiência em C#.',
  adaptedText: null,
  suggestions: [
    {
      id: 'sug-1',
      level: 0, // Safe
      originalText: 'Desenvolvedor Backend',
      proposedText: 'Engenheiro de Software Backend Pleno',
      reason: 'Adequação terminológica',
      evidence: '5 anos de experiência em C#',
    },
  ],
  createdAt: '2026-09-08T00:00:00Z',
  updatedAt: '2026-09-08T00:00:00Z',
}

describe('OptimizationAdaptationPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('renders comparative side-by-side view with original text and suggestion cards', async () => {
    vi.mocked(optimizationService.getOptimizationPlan).mockResolvedValue(mockPlanDraft as any)

    render(
      <MemoryRouter initialEntries={['/app/adaptacoes/plan-adapt-123']}>
        <Routes>
          <Route
            path="/app/adaptacoes/:optimizationId"
            element={<OptimizationAdaptationPage />}
          />
        </Routes>
      </MemoryRouter>
    )

    await waitFor(() => {
      expect(screen.getByText('Currículo Original')).toBeInTheDocument()
    })

    // Original text
    expect(
      screen.getByText('João Silva - Desenvolvedor Backend com 5 anos de experiência em C#.')
    ).toBeInTheDocument()

    // Suggestion card (quotes around proposed text)
    expect(screen.getByText(/Engenheiro de Software Backend Pleno/i)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Aplicar alterações aprovadas/i })).toBeInTheDocument()
  })

  it('calls applyOptimization and displays server-returned adapted text with export buttons', async () => {
    vi.mocked(optimizationService.getOptimizationPlan).mockResolvedValue(mockPlanDraft as any)
    vi.mocked(optimizationService.applyOptimization).mockResolvedValue({
      id: 'plan-adapt-123',
      analysisId: 'analysis-123',
      resumeId: 'resume-123',
      version: 2,
      status: 'Applied',
      originalText: mockPlanDraft.originalText,
      adaptedText: 'João Silva - Engenheiro de Software Backend Pleno com 5 anos de experiência em C#.',
      appliedChanges: [
        {
          suggestionId: 'sug-1',
          level: 0,
          originalText: 'Desenvolvedor Backend',
          proposedText: 'Engenheiro de Software Backend Pleno',
          userDeclaration: null,
          reason: 'Adequação terminológica',
          informationOrigin: 'BaseadaNoCurriculo',
        },
      ],
      createdAt: '2026-09-08T00:00:00Z',
      updatedAt: '2026-09-08T00:00:00Z',
    } as any)

    render(
      <MemoryRouter initialEntries={['/app/adaptacoes/plan-adapt-123']}>
        <Routes>
          <Route
            path="/app/adaptacoes/:optimizationId"
            element={<OptimizationAdaptationPage />}
          />
        </Routes>
      </MemoryRouter>
    )

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /Aplicar alterações aprovadas/i })).toBeInTheDocument()
    })

    const applyBtn = screen.getByRole('button', { name: /Aplicar alterações aprovadas/i })
    fireEvent.click(applyBtn)

    await waitFor(() => {
      expect(optimizationService.applyOptimization).toHaveBeenCalledWith(
        'plan-adapt-123',
        1,
        expect.any(Array)
      )
    })

    // Server-returned adapted text is rendered
    await waitFor(() => {
      expect(
        screen.getByDisplayValue(
          'João Silva - Engenheiro de Software Backend Pleno com 5 anos de experiência em C#.'
        )
      ).toBeInTheDocument()
    })

    // Export buttons appear
    expect(screen.getByRole('button', { name: /PDF — modelo padrão/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /DOCX — modelo padrão/i })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: /DOCX com layout original/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Copiar Texto Completo/i })).toBeInTheDocument()
  })

  it('handles 409 Conflict with informative notice and reload option', async () => {
    vi.mocked(optimizationService.getOptimizationPlan).mockResolvedValue(mockPlanDraft as any)

    const conflictError = new ApiError(409, 'Versão desatualizada', {
      title: 'Conflict',
      status: 409,
      detail: 'O plano foi atualizado por outro processo.',
    })
    vi.mocked(optimizationService.applyOptimization).mockRejectedValue(conflictError)

    render(
      <MemoryRouter initialEntries={['/app/adaptacoes/plan-adapt-123']}>
        <Routes>
          <Route
            path="/app/adaptacoes/:optimizationId"
            element={<OptimizationAdaptationPage />}
          />
        </Routes>
      </MemoryRouter>
    )

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /Aplicar alterações aprovadas/i })).toBeInTheDocument()
    })

    const applyBtn = screen.getByRole('button', { name: /Aplicar alterações aprovadas/i })
    fireEvent.click(applyBtn)

    await waitFor(() => {
      expect(screen.getByText(/Conflito de Versão \(409\)/i)).toBeInTheDocument()
      expect(screen.getByRole('button', { name: /Recarregar versão mais recente/i })).toBeInTheDocument()
    })

    // Test clicking reload
    const reloadBtn = screen.getByRole('button', { name: /Recarregar versão mais recente/i })
    fireEvent.click(reloadBtn)

    await waitFor(() => {
      expect(optimizationService.getOptimizationPlan).toHaveBeenCalledTimes(2)
    })
  })

  it('triggers PDF export download via exportAdaptedResume', async () => {
    const appliedPlan = {
      ...mockPlanDraft,
      status: 'Applied',
      adaptedText: 'Texto adaptado pronto para exportação.',
      version: 2,
    }
    vi.mocked(optimizationService.getOptimizationPlan).mockResolvedValue(appliedPlan as any)
    vi.mocked(optimizationService.exportAdaptedResume).mockResolvedValue()

    render(
      <MemoryRouter initialEntries={['/app/adaptacoes/plan-adapt-123']}>
        <Routes>
          <Route
            path="/app/adaptacoes/:optimizationId"
            element={<OptimizationAdaptationPage />}
          />
        </Routes>
      </MemoryRouter>
    )

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /PDF — modelo padrão/i })).toBeInTheDocument()
    })

    const pdfBtn = screen.getByRole('button', { name: /PDF — modelo padrão/i })
    fireEvent.click(pdfBtn)

    await waitFor(() => {
      expect(optimizationService.exportAdaptedResume).toHaveBeenCalledWith('plan-adapt-123', 'pdf')
    })
  })

  it('triggers DOCX export download via exportAdaptedResume', async () => {
    const appliedPlan = {
      ...mockPlanDraft,
      status: 'Applied',
      adaptedText: 'Texto adaptado pronto para exportação.',
      version: 2,
    }
    vi.mocked(optimizationService.getOptimizationPlan).mockResolvedValue(appliedPlan as any)
    vi.mocked(optimizationService.exportAdaptedResume).mockResolvedValue()

    render(
      <MemoryRouter initialEntries={['/app/adaptacoes/plan-adapt-123']}>
        <Routes>
          <Route
            path="/app/adaptacoes/:optimizationId"
            element={<OptimizationAdaptationPage />}
          />
        </Routes>
      </MemoryRouter>
    )

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /DOCX — modelo padrão/i })).toBeInTheDocument()
    })

    const docxBtn = screen.getByRole('button', { name: /DOCX — modelo padrão/i })
    fireEvent.click(docxBtn)

    await waitFor(() => {
      expect(optimizationService.exportAdaptedResume).toHaveBeenCalledWith('plan-adapt-123', 'docx')
    })
  })
})
