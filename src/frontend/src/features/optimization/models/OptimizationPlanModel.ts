export type OptimizationSafetyLevel = 'Safe' | 'NeedsConfirmation' | 'Forbidden'

export function normalizeSafetyLevel(level: unknown): OptimizationSafetyLevel {
  if (level === 0 || level === '0' || level === 'Safe' || level === 'safe') return 'Safe'
  if (level === 1 || level === '1' || level === 'NeedsConfirmation' || level === 'needsconfirmation') return 'NeedsConfirmation'
  if (level === 2 || level === '2' || level === 'Forbidden' || level === 'forbidden') return 'Forbidden'
  return 'NeedsConfirmation'
}

export interface OptimizationSuggestionModel {
  id: string
  level: OptimizationSafetyLevel | number
  confirmed: boolean
  originalText: string
  proposedText: string
  reason: string
  evidence?: string | null
  confirmationQuestion?: string | null
}

export interface OptimizationPlanModel {
  id: string
  analysisId: string
  resumeId: string
  version: number
  status: string
  originalText: string
  suggestions: OptimizationSuggestionModel[]
  createdAt: string
  adaptedText?: string | null
}

export interface OptimizationDecisionModel {
  suggestionId: string
  accepted: boolean
  confirmed?: boolean
  userDeclaration?: string | null
}

export interface AppliedOptimizationItemModel {
  suggestionId: string
  level: OptimizationSafetyLevel | number
  originalText: string
  proposedText: string
  reason: string
  wasConfirmed: boolean
  informationOrigin?: string
  userDeclaration?: string | null
}

export interface OptimizationResultModel {
  id: string
  analysisId: string
  resumeId: string
  originalText: string
  adaptedText: string
  appliedChanges: AppliedOptimizationItemModel[]
  updatedAt: string
}
