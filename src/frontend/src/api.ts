import type { AnalysisResult } from './types'

const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/+$/, '')

async function parse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const problem = await response.json().catch(() => null)
    throw new Error(problem?.title ?? 'Não foi possível concluir a solicitação.')
  }
  return response.json() as Promise<T>
}

export async function uploadResume(file: File): Promise<string> {
  const data = new FormData()
  data.append('file', file)
  const result = await parse<{ id: string }>(await fetch(`${apiBaseUrl}/api/resumes/upload`, { method: 'POST', body: data }))
  return result.id
}

export async function compareResume(resumeId: string, jobDescription: string): Promise<AnalysisResult> {
  return parse(await fetch(`${apiBaseUrl}/api/analysis/compare`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ resumeId, jobDescription })
  }))
}
