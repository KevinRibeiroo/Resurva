import { authenticatedFetch, handleApiResponse, ApiError } from '../../../shared/api/httpClient'
import type {
  OptimizationPlanModel,
  OptimizationDecisionModel,
  OptimizationResultModel,
} from '../models/OptimizationPlanModel'

export async function createOptimizationPlan(analysisId: string): Promise<OptimizationPlanModel> {
  const response = await authenticatedFetch(`/api/analysis/${analysisId}/optimization`, {
    method: 'POST',
  })

  return handleApiResponse<OptimizationPlanModel>(response)
}

export async function getOptimizationPlan(id: string): Promise<OptimizationPlanModel> {
  const response = await authenticatedFetch(`/api/optimizations/${id}`, {
    method: 'GET',
  })

  return handleApiResponse<OptimizationPlanModel>(response)
}

export async function applyOptimization(
  id: string,
  version: number,
  decisions: OptimizationDecisionModel[]
): Promise<OptimizationResultModel> {
  const response = await authenticatedFetch(`/api/optimizations/${id}/apply`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ version, decisions }),
  })

  return handleApiResponse<OptimizationResultModel>(response)
}

export async function exportAdaptedResume(id: string, format: 'pdf' | 'docx'): Promise<void> {
  const response = await authenticatedFetch(`/api/optimizations/${id}/export?format=${format}`, {
    method: 'GET',
  })

  if (!response.ok) {
    const problem = await response.json().catch(() => null)
    let message = problem?.detail || problem?.title

    if (!message) {
      if (response.status === 401) message = 'Sua sessão expirou. Entre novamente para baixar.'
      else if (response.status === 403) message = 'Acesso não autorizado para download.'
      else if (response.status === 409) message = 'O currículo precisa ser adaptado e aplicado antes de exportar.'
      else message = `Falha ao exportar documento em formato ${format.toUpperCase()}.`
    }

    throw new ApiError(response.status, message, problem)
  }

  const blob = await response.blob()
  const downloadUrl = window.URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = downloadUrl
  anchor.download = `curriculo-adaptado.${format}`
  document.body.appendChild(anchor)
  anchor.click()
  anchor.remove()
  window.URL.revokeObjectURL(downloadUrl)
}
