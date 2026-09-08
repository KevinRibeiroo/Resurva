import React from 'react'
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { MemoryRouter, Routes, Route } from 'react-router-dom'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { AnalysisResultPage } from '../features/analysis/pages/AnalysisResultPage'
import * as analysisService from '../features/analysis/services/analysisService'
import * as optimizationService from '../features/optimization/services/optimizationService'

const mockNavigate = vi.fn()
vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual('react-router-dom')
  return {
    ...actual,
    useNavigate: () => mockNavigate,
  }
})

vi.mock('../features/analysis/services/analysisService', () => ({
  getAnalysis: vi.fn(),
  deleteResume: vi.fn(),
  uploadResume: vi.fn(),
  compareResume: vi.fn(),
}))

vi.mock('../features/optimization/services/optimizationService', () => ({
  createOptimizationPlan: vi.fn(),
  getOptimizationPlan: vi.fn(),
  applyOptimization: vi.fn(),
  exportAdaptedResume: vi.fn(),
}))

const mockAnalysisResult = {
  id: 'analysis-uuid-12345678',
  resumeId: 'resume-uuid-1',
  overallScore: 82,
  skillsScore: 85,
  experienceScore: 78,
  seniorityScore: 90,
  requirementsScore: 80,
  educationScore: 75,
  matchedSkills: [
    { text: 'C# / .NET', evidence: '5 anos de experiência com C# e .NET Core' },
    { text: 'PostgreSQL', evidence: 'Modelagem e queries em PostgreSQL' },
  ],
  missingSkills: [{ text: 'Kubernetes', evidence: null }],
  requirementsMet: [{ text: 'Ensino Superior em Computação', evidence: 'Bacharelado em Ciência da Computação' }],
  requirementsMissing: [{ text: 'Certificação AWS ou GCP', evidence: null }],
  strengths: [{ text: 'Forte base em backend e arquitetura limpa', evidence: 'Projetos com DDD e CQRS' }],
  pointsOfAttention: [{ text: 'Pouca exposição a orquestradores de contêineres', evidence: null }],
  recommendations: [{ text: 'Destacar vivência prática com Docker em projetos recentes', evidence: null }],
  createdAt: '2026-09-08T00:00:00Z',
}

describe('AnalysisResultPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('renders overall score and all 5 official scoring dimensions', async () => {
    vi.mocked(analysisService.getAnalysis).mockResolvedValue(mockAnalysisResult as any)

    render(
      <MemoryRouter initialEntries={['/app/analises/analysis-uuid-12345678']}>
        <Routes>
          <Route path="/app/analises/:analysisId" element={<AnalysisResultPage />} />
        </Routes>
      </MemoryRouter>
    )

    // Wait for analysis to load
    await waitFor(() => {
      expect(screen.getByText('Resultado da Análise de Aderência')).toBeInTheDocument()
    })

    // Overall score 82 and compatibility label
    expect(screen.getByText('82')).toBeInTheDocument()
    expect(screen.getByText(/Índice Geral de Compatibilidade/i)).toBeInTheDocument()

    // 5 dimensions
    expect(screen.getByText('Skills & Competências')).toBeInTheDocument()
    expect(screen.getByText('85')).toBeInTheDocument()

    expect(screen.getByText('Experiência Prática')).toBeInTheDocument()
    expect(screen.getByText('78')).toBeInTheDocument()

    expect(screen.getByText('Senioridade Requerida')).toBeInTheDocument()
    expect(screen.getByText('90')).toBeInTheDocument()

    expect(screen.getByText('Requisitos Formais')).toBeInTheDocument()
    expect(screen.getByText('80')).toBeInTheDocument()

    expect(screen.getByText('Formação e Educação')).toBeInTheDocument()
    expect(screen.getByText('75')).toBeInTheDocument()
  })

  it('renders evidence snippets and expands interactive chip when clicked', async () => {
    vi.mocked(analysisService.getAnalysis).mockResolvedValue(mockAnalysisResult as any)

    render(
      <MemoryRouter initialEntries={['/app/analises/analysis-uuid-12345678']}>
        <Routes>
          <Route path="/app/analises/:analysisId" element={<AnalysisResultPage />} />
        </Routes>
      </MemoryRouter>
    )

    await waitFor(() => {
      expect(screen.getByText('C# / .NET')).toBeInTheDocument()
    })

    // Strengths evidence is rendered directly
    expect(screen.getByText('Forte base em backend e arquitetura limpa')).toBeInTheDocument()
    expect(screen.getByText(/Projetos com DDD e CQRS/i)).toBeInTheDocument()

    // Interactive evidence chip: click to expand evidence
    const skillChip = screen.getByRole('button', { name: /C# \/ \.NET/i })
    fireEvent.click(skillChip)

    expect(screen.getByText(/5 anos de experiência com C# e .NET Core/i)).toBeInTheDocument()
  })

  it('navigates to confirmations page if optimization plan contains NeedsConfirmation items', async () => {
    vi.mocked(analysisService.getAnalysis).mockResolvedValue(mockAnalysisResult as any)
    vi.mocked(optimizationService.createOptimizationPlan).mockResolvedValue({
      id: 'opt-plan-1',
      analysisId: 'analysis-uuid-12345678',
      resumeId: 'resume-uuid-1',
      version: 1,
      status: 'Draft',
      originalText: 'Curriculo original',
      adaptedText: null,
      suggestions: [
        {
          id: 'sug-1',
          level: 1, // NeedsConfirmation
          originalText: null,
          proposedText: 'Experiência com Docker em produção',
          reason: 'Vaga valoriza Docker',
          evidence: null,
        },
      ],
      createdAt: '2026-09-08T00:00:00Z',
      updatedAt: '2026-09-08T00:00:00Z',
    } as any)

    render(
      <MemoryRouter initialEntries={['/app/analises/analysis-uuid-12345678']}>
        <Routes>
          <Route path="/app/analises/:analysisId" element={<AnalysisResultPage />} />
        </Routes>
      </MemoryRouter>
    )

    await waitFor(() => {
      expect(screen.getAllByRole('button', { name: /Adaptar currículo para esta vaga/i }).length).toBeGreaterThan(0)
    })

    const optBtn = screen.getAllByRole('button', { name: /Adaptar currículo para esta vaga/i })[0]
    fireEvent.click(optBtn)

    await waitFor(() => {
      expect(optimizationService.createOptimizationPlan).toHaveBeenCalledWith('analysis-uuid-12345678')
      expect(mockNavigate).toHaveBeenCalledWith('/app/adaptacoes/opt-plan-1/confirmacoes', expect.any(Object))
    })
  })
})
