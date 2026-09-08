import React from 'react'
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { NewAnalysisPage } from '../features/analysis/pages/NewAnalysisPage'
import * as analysisService from '../features/analysis/services/analysisService'

const mockNavigate = vi.fn()
vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual('react-router-dom')
  return {
    ...actual,
    useNavigate: () => mockNavigate,
  }
})

vi.mock('../features/analysis/services/analysisService', () => ({
  uploadResume: vi.fn(),
  compareResume: vi.fn(),
  getAnalysis: vi.fn(),
  deleteResume: vi.fn(),
}))

describe('NewAnalysisPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('renders both pillars (Resume and Job Description) and action button', () => {
    render(
      <MemoryRouter>
        <NewAnalysisPage />
      </MemoryRouter>
    )

    expect(screen.getByText('Seu Currículo')).toBeInTheDocument()
    expect(screen.getByText('Descrição da Vaga')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Comparar currículo com vaga/i })).toBeInTheDocument()
  })

  it('validates required file and job description before executing', async () => {
    render(
      <MemoryRouter>
        <NewAnalysisPage />
      </MemoryRouter>
    )

    const submitBtn = screen.getByRole('button', { name: /Comparar currículo com vaga/i })
    fireEvent.click(submitBtn)

    expect(
      screen.getByText(/Por favor, selecione ou arraste um arquivo de currículo em PDF ou DOCX/i)
    ).toBeInTheDocument()
    expect(
      screen.getByText(/Por favor, cole a descrição da vaga com os requisitos e qualificações/i)
    ).toBeInTheDocument()

    expect(analysisService.uploadResume).not.toHaveBeenCalled()
    expect(analysisService.compareResume).not.toHaveBeenCalled()
  })

  it('uploads resume then compares with job description and navigates to result', async () => {
    const fakeFile = new File(['fake resume text content'], 'curriculo.pdf', {
      type: 'application/pdf',
    })
    const fakeResult = {
      id: 'analysis-123',
      resumeId: 'resume-999',
      overallScore: 88,
      skillsScore: 90,
      experienceScore: 85,
      seniorityScore: 80,
      requirementsScore: 95,
      educationScore: 90,
      matchedSkills: [],
      missingSkills: [],
      requirementsMet: [],
      requirementsMissing: [],
      strengths: [],
      pointsOfAttention: [],
      recommendations: [],
      createdAt: new Date().toISOString(),
    }

    vi.mocked(analysisService.uploadResume).mockResolvedValue('resume-999')
    vi.mocked(analysisService.compareResume).mockResolvedValue(fakeResult as any)

    render(
      <MemoryRouter>
        <NewAnalysisPage />
      </MemoryRouter>
    )

    // Select file via testid
    const fileInput = screen.getByTestId('resume-file-input')
    fireEvent.change(fileInput, { target: { files: [fakeFile] } })

    // Enter job description via testid
    const textarea = screen.getByTestId('job-description-input')
    fireEvent.change(textarea, {
      target: { value: 'Desenvolvedor C# e React com sólida experiência em arquitetura de microsserviços.' },
    })

    const submitBtn = screen.getByRole('button', { name: /Comparar currículo com vaga/i })
    fireEvent.click(submitBtn)

    // Verify uploadResume was called with the file
    await waitFor(() => {
      expect(analysisService.uploadResume).toHaveBeenCalledWith(fakeFile)
    })

    // Verify compareResume was called with resumeId and job description
    await waitFor(() => {
      expect(analysisService.compareResume).toHaveBeenCalledWith(
        'resume-999',
        'Desenvolvedor C# e React com sólida experiência em arquitetura de microsserviços.'
      )
    })

    // Verify navigation
    await waitFor(() => {
      expect(mockNavigate).toHaveBeenCalledWith('/app/analises/analysis-123', {
        state: { analysisResult: fakeResult },
      })
    })
  })

  it('displays character counter and enforces max 75.000 characters limit', () => {
    render(
      <MemoryRouter>
        <NewAnalysisPage />
      </MemoryRouter>
    )

    const textarea = screen.getByTestId('job-description-input')
    fireEvent.change(textarea, { target: { value: 'Texto de teste' } })

    expect(screen.getByText(/14 \/ 75\.000 caracteres/i)).toBeInTheDocument()
  })
})
