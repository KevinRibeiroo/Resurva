import { render, screen, fireEvent } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { EvidenceWorkbench } from '../features/analysis/components/EvidenceWorkbench'
import type { AnalysisResultModel } from '../features/analysis/models/AnalysisResultModel'

const analysis: AnalysisResultModel = {
  id:'synthetic', resumeId:'synthetic', overallScore:80, skillsScore:80, experienceScore:80,
  seniorityScore:80, requirementsScore:80, educationScore:80,
  matchedSkills:[{text:'React',evidence:'Projeto demonstrativo com React.'},{text:'TypeScript',evidence:null}],
  missingSkills:[{text:'Kubernetes',evidence:null}], requirementsMet:[], requirementsMissing:[],
  strengths:[], pointsOfAttention:[], recommendations:[],
}

describe('EvidenceWorkbench', () => {
  it('replaces evidence with an honest missing state instead of carrying over a previous quote', () => {
    render(<EvidenceWorkbench analysis={analysis} />)
    expect(screen.getByText('Projeto demonstrativo com React.')).toBeVisible()
    fireEvent.click(screen.getByRole('button',{name:/Kubernetes/}))
    expect(screen.queryByText('Projeto demonstrativo com React.')).not.toBeInTheDocument()
    expect(screen.getByText('Nenhuma evidência foi encontrada para este item no currículo enviado.')).toBeVisible()
    expect(screen.getByRole('button',{name:/Kubernetes/})).toHaveAttribute('aria-expanded','true')
  })
  it('supports keyboard selection and identifies found items without supporting excerpts', async () => {
    const user = userEvent.setup()
    render(<EvidenceWorkbench analysis={analysis} />)
    await user.tab()
    await user.tab()
    await user.keyboard('{Enter}')
    expect(screen.getByRole('button',{name:/TypeScript/})).toHaveFocus()
    expect(screen.getByText('A análise identificou este item, mas não retornou um trecho de apoio.')).toBeVisible()
  })
  it('explains when no requirements were returned', () => {
    render(<EvidenceWorkbench analysis={{...analysis,matchedSkills:[],missingSkills:[]}} />)
    expect(screen.getByText(/não retornou requisitos para comparar/)).toBeVisible()
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
  })
})
