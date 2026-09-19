import { authenticatedFetch, handleApiResponse } from '../../../shared/api/httpClient'
import type { LayoutInspectionModel } from '../models/LayoutInspectionModel'

export async function inspectOriginalLayout(id: string, file: File, signal: AbortSignal): Promise<LayoutInspectionModel> {
  const form = new FormData()
  form.append('file', file)
  const response = await authenticatedFetch(`/api/optimizations/${id}/layout/inspect`, { method: 'POST', body: form, signal })
  return handleApiResponse<LayoutInspectionModel>(response)
}

export async function exportOriginalLayout(id: string, file: File, inspection: LayoutInspectionModel,
  targets: Record<string, string>, signal: AbortSignal): Promise<Blob> {
  const form = new FormData()
  form.append('file', file)
  form.append('version', String(inspection.version))
  form.append('sourceSha256', inspection.sourceSha256)
  form.append('placements', JSON.stringify(inspection.changes.filter(change =>
    !change.blockedReason && change.candidates.some(block => block.id === targets[change.suggestionId])
  ).map(change => ({
    suggestionId: change.suggestionId, blockId: targets[change.suggestionId],
  }))))
  const response = await authenticatedFetch(`/api/optimizations/${id}/layout/export`, { method: 'POST', body: form, signal })
  if (!response.ok) await handleApiResponse(response)
  return response.blob()
}
