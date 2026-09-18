export interface LayoutInspectionModel {
  version: number
  sourceSha256: string
  reviewStatus: string
  changes: {
    suggestionId: string
    proposedText: string
    kind: string
    blockedReason: string | null
    candidates: { id: string; text: string; section: string | null }[]
  }[]
}
