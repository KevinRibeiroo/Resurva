import { authenticatedFetch, handleApiResponse } from '../../../shared/api/httpClient'
import type { AnalysisResultModel } from '../models/AnalysisResultModel'

export async function uploadResume(file: File): Promise<string> {
  const formData = new FormData()
  formData.append('file', file)

  const response = await authenticatedFetch('/api/resumes/upload', {
    method: 'POST',
    body: formData,
  })

  const result = await handleApiResponse<{ id: string }>(response)
  return result.id
}

export async function compareResume(resumeId: string, jobDescription: string): Promise<AnalysisResultModel> {
  const response = await authenticatedFetch('/api/analysis/compare', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ resumeId, jobDescription }),
  })

  return handleApiResponse<AnalysisResultModel>(response)
}

export async function getAnalysis(id: string): Promise<AnalysisResultModel> {
  const response = await authenticatedFetch(`/api/analysis/${id}`, {
    method: 'GET',
  })

  return handleApiResponse<AnalysisResultModel>(response)
}

export async function deleteResume(id: string): Promise<void> {
  const response = await authenticatedFetch(`/api/resumes/${id}`, {
    method: 'DELETE',
  })

  return handleApiResponse<void>(response)
}
