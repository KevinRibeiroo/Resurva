import { getCurrentIdToken } from '../../features/auth/services/authService'

export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  instance?: string
  errors?: Record<string, string[]>
}

export class ApiError extends Error {
  public status: number
  public problem: ProblemDetails | null

  constructor(status: number, message: string, problem: ProblemDetails | null = null) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.problem = problem
  }

  get isConflict(): boolean {
    return this.status === 409
  }

  get isNotFound(): boolean {
    return this.status === 404
  }

  get isUnauthorized(): boolean {
    return this.status === 401
  }

  get isForbidden(): boolean {
    return this.status === 403
  }
}

const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/+$/, '')

export async function authenticatedFetch(path: string, options: RequestInit = {}): Promise<Response> {
  const headers = new Headers(options.headers)
  const token = await getCurrentIdToken()
  headers.set('Authorization', `Bearer ${token}`)
  return fetch(`${apiBaseUrl}${path}`, { ...options, headers })
}

export async function handleApiResponse<T>(response: Response): Promise<T> {
  if (response.status === 204) {
    return undefined as unknown as T
  }

  if (!response.ok) {
    const problem = await response.json().catch(() => null) as ProblemDetails | null

    let message = problem?.detail || problem?.title

    if (!message) {
      switch (response.status) {
        case 401:
          message = 'Sua sessão não foi aceita ou expirou. Saia e entre novamente.'
          break
        case 403:
          message = 'Esta conta não tem acesso ao ambiente privado de testes.'
          break
        case 404:
          message = 'Registro não encontrado ou você não possui permissão para acessá-lo.'
          break
        case 409:
          message = 'O plano de otimização foi atualizado no servidor. Por favor, recarregue a página para obter a versão mais recente.'
          break
        case 413:
          message = 'O arquivo ou texto enviado excede o limite máximo permitido pelo servidor.'
          break
        case 429:
          message = 'Muitas requisições enviadas em curto intervalo. Aguarde alguns instantes e tente novamente.'
          break
        case 502:
        case 503:
        case 504:
          message = 'O serviço de inteligência ou banco de dados está temporariamente indisponível. Tente novamente em instantes.'
          break
        default:
          message = 'Ocorreu uma falha ao processar sua solicitação no servidor.'
          break
      }
    }

    throw new ApiError(response.status, message, problem)
  }

  return response.json() as Promise<T>
}
