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
