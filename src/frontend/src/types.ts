export type EvidenceItem = { text: string; evidence?: string }

export type AnalysisResult = {
  id: string
  resumeId: string
  overallScore: number
  skillsScore: number
  experienceScore: number
  seniorityScore: number
  requirementsScore: number
  educationScore: number
  matchedSkills: EvidenceItem[]
  missingSkills: EvidenceItem[]
  requirementsMet: EvidenceItem[]
  requirementsMissing: EvidenceItem[]
  strengths: EvidenceItem[]
  pointsOfAttention: EvidenceItem[]
  recommendations: EvidenceItem[]
}

export type OptimizationSafetyLevel = 'Safe' | 'NeedsConfirmation' | 'Forbidden'

export type OptimizationSuggestion = {
  id: string
  level: OptimizationSafetyLevel | number
  confirmed: boolean
  originalText: string
  proposedText: string
  reason: string
  evidence?: string | null
  confirmationQuestion?: string | null
}

export type OptimizationPlan = {
  id: string
  analysisId: string
  resumeId: string
  version: number
  status: string
  originalText: string
  suggestions: OptimizationSuggestion[]
  createdAt: string
  adaptedText?: string | null
}

export type OptimizationDecision = {
  suggestionId: string
  accepted: boolean
  confirmed?: boolean
  userDeclaration?: string
}

export type AppliedOptimizationItem = {
  suggestionId: string
  level: OptimizationSafetyLevel | number
  originalText: string
  proposedText: string
  reason: string
  wasConfirmed: boolean
  informationOrigin?: string
  userDeclaration?: string
}

export type OptimizationResult = {
  id: string
  analysisId: string
  resumeId: string
  originalText: string
  adaptedText: string
  appliedChanges: AppliedOptimizationItem[]
  updatedAt: string
}
