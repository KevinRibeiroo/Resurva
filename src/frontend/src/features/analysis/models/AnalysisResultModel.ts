export interface EvidenceItemModel {
  text: string
  evidence?: string | null
}

export interface AnalysisResultModel {
  id: string
  resumeId: string
  overallScore: number
  skillsScore: number
  experienceScore: number
  seniorityScore: number
  requirementsScore: number
  educationScore: number
  matchedSkills: EvidenceItemModel[]
  missingSkills: EvidenceItemModel[]
  requirementsMet: EvidenceItemModel[]
  requirementsMissing: EvidenceItemModel[]
  strengths: EvidenceItemModel[]
  pointsOfAttention: EvidenceItemModel[]
  recommendations: EvidenceItemModel[]
}
