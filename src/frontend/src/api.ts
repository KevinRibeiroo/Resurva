import type { AnalysisResult } from './types'
import { getCurrentIdToken } from './auth'

const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/+$/, '')

async function parse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    if (response.status === 401) throw new Error('Sua sessão não foi aceita. Saia e entre novamente.')
    if (response.status === 403) throw new Error('Esta conta não tem acesso ao ambiente de testes.')
    const problem = await response.json().catch(() => null)
    throw new Error(problem?.title ?? 'Não foi possível concluir a solicitação.')
  }
  return response.json() as Promise<T>
}

async function authenticatedFetch(path: string, options: RequestInit = {}): Promise<Response> {
  const headers = new Headers(options.headers)
  headers.set('Authorization', `Bearer ${await getCurrentIdToken()}`)
  return fetch(`${apiBaseUrl}${path}`, { ...options, headers })
}

export async function verifySession(): Promise<void> {
  const response = await authenticatedFetch('/api/auth/session')
  if (response.status !== 204) {
    await parse(response)
    throw new Error('A API não confirmou o acesso. Confira a versão publicada.')
  }
}

export async function uploadResume(file: File): Promise<string> {
  const data = new FormData()
  data.append('file', file)
  const result = await parse<{ id: string }>(await authenticatedFetch('/api/resumes/upload', { method: 'POST', body: data }))
  return result.id
}

export async function compareResume(resumeId: string, jobDescription: string): Promise<AnalysisResult> {
  return parse(await authenticatedFetch('/api/analysis/compare', {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ resumeId, jobDescription })
  }))
}

export async function createOptimizationPlan(analysisId: string): Promise<import('./types').OptimizationPlan> {
  return parse(await authenticatedFetch(`/api/analysis/${analysisId}/optimization`, {
    method: 'POST'
  }))
}

export async function getOptimizationPlan(id: string): Promise<import('./types').OptimizationPlan> {
  return parse(await authenticatedFetch(`/api/optimizations/${id}`, {
    method: 'GET'
  }))
}

export async function applyOptimization(
  id: string,
  version: number,
  decisions: import('./types').OptimizationDecision[]
): Promise<import('./types').OptimizationResult> {
  return parse(await authenticatedFetch(`/api/optimizations/${id}/apply`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ version, decisions })
  }))
}
