import React from 'react'
import { render, screen, fireEvent, waitFor, act } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { LayoutExportPanel } from '../features/optimization/components/LayoutExportPanel'

vi.mock('../features/auth/services/authService', () => ({ getCurrentIdToken: async () => 'synthetic-token' }))

const inspection = {
  version: 2, sourceSha256: 'synthetic-hash', reviewStatus: 'visual_review_pending',
  changes: [{ suggestionId: 'suggestion-1', proposedText: 'PostgreSQL', kind: 'insert_skill', blockedReason: null,
    candidates: [{ id: 'p:6', text: 'Bancos: MySQL.', section: 'HABILIDADES' }] }],
}

afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals() })

describe('exportação com layout original', () => {
  it('ignora uma inspeção atrasada após trocar o arquivo', async () => {
    let resolveResponse!: (value: Response) => void
    const response = new Promise<Response>(resolve => { resolveResponse = resolve })
    const fetchMock = vi.fn().mockReturnValue(response)
    vi.stubGlobal('fetch', fetchMock)
    render(<LayoutExportPanel optimizationId="plan-1" resumeId="resume-1" />)
    fireEvent.change(screen.getByLabelText('DOCX original'), { target: { files: [new File(['a'], 'original.docx')] } })
    fireEvent.click(screen.getByRole('button', { name: 'Inspecionar DOCX' }))
    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(1))
    fireEvent.change(screen.getByLabelText('DOCX original'), { target: { files: [new File(['b'], 'novo.docx')] } })
    await act(async () => { resolveResponse(new Response(JSON.stringify(inspection))); await response })
    expect(fetchMock.mock.calls[0][1].signal.aborted).toBe(true)
    expect(screen.queryByText('PostgreSQL')).not.toBeInTheDocument()
    expect(screen.getByText('Selecionado: novo.docx')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Inspecionar DOCX' })).toBeEnabled()
  })
  it('inspeciona, exige destino explícito de skill e baixa usando somente IDs e versão', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(new Response(JSON.stringify(inspection)))
      .mockResolvedValueOnce(new Response(new Blob(['synthetic-docx'])))
    vi.stubGlobal('fetch', fetchMock)
    const create = vi.fn(() => 'blob:synthetic')
    vi.stubGlobal('URL', Object.assign(URL, { createObjectURL: create, revokeObjectURL: vi.fn() }))
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {})
    render(<LayoutExportPanel optimizationId="plan-1" resumeId="resume-1" />)
    const file = new File(['synthetic'], 'original.docx')
    fireEvent.change(screen.getByLabelText('DOCX original'), { target: { files: [file] } })
    fireEvent.click(screen.getByRole('button', { name: 'Inspecionar DOCX' }))
    await screen.findByText('PostgreSQL')
    const download = screen.getByRole('button', { name: 'Baixar DOCX com layout original' })
    expect(download).toBeDisabled()
    fireEvent.change(screen.getByLabelText('Destino de PostgreSQL'), { target: { value: 'p:6' } })
    fireEvent.click(download)
    await waitFor(() => expect(create).toHaveBeenCalledTimes(1))
    const [url, request] = fetchMock.mock.calls[1]
    expect(url).toContain('/api/optimizations/plan-1/layout/export')
    expect(request.headers.get('Authorization')).toBe('Bearer synthetic-token')
    expect(request.headers.has('Content-Type')).toBe(false)
    expect(request.body.get('file')).toBe(file)
    expect(JSON.parse(request.body.get('placements'))).toEqual([{ suggestionId: 'suggestion-1', blockId: 'p:6' }])
    expect(request.body.get('version')).toBe('2')
    expect(request.body.get('sourceSha256')).toBe('synthetic-hash')
    expect(await screen.findByText(/Download iniciado/)).toBeInTheDocument()
  })

  it('preserva arquivo para nova tentativa após erro e invalida inspeção ao trocá-lo', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(new Response(JSON.stringify({ title: 'Arquivo diferente do original.' }), { status: 409 }))
      .mockResolvedValueOnce(new Response(JSON.stringify(inspection)))
    vi.stubGlobal('fetch', fetchMock)
    render(<LayoutExportPanel optimizationId="plan-1" resumeId="resume-1" />)
    fireEvent.change(screen.getByLabelText('DOCX original'), { target: { files: [new File(['a'], 'original.docx')] } })
    fireEvent.click(screen.getByRole('button', { name: 'Inspecionar DOCX' }))
    await screen.findByText('Arquivo diferente do original.')
    fireEvent.click(screen.getByRole('button', { name: 'Inspecionar DOCX' }))
    await screen.findByText('PostgreSQL')
    fireEvent.change(screen.getByLabelText('DOCX original'), { target: { files: [new File(['b'], 'outro.docx')] } })
    expect(screen.queryByText('PostgreSQL')).not.toBeInTheDocument()
  })

  it('desabilita download quando nenhuma alteração tem destino válido', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({ ...inspection,
      changes: [{ ...inspection.changes[0], candidates: [], blockedReason: 'Formatação não suportada.' }] }))))
    render(<LayoutExportPanel optimizationId="plan-1" resumeId="resume-1" />)
    fireEvent.change(screen.getByLabelText('DOCX original'), { target: { files: [new File(['a'], 'original.docx')] } })
    fireEvent.click(screen.getByRole('button', { name: 'Inspecionar DOCX' }))
    await screen.findByText('Formatação não suportada.')
    expect(screen.getByText('Não incluídas neste download')).toBeInTheDocument()
    expect(screen.queryByText(/Resolva os destinos pendentes/)).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Baixar DOCX com layout original' })).toBeDisabled()
  })

  it('exporta alterações com destino mesmo quando outra está bloqueada ou não foi selecionada', async () => {
    const partial = { ...inspection, changes: [inspection.changes[0],
      { ...inspection.changes[0], suggestionId: 'blocked', proposedText: 'Título sintético', candidates: [], blockedReason: 'Sem destino compatível.' },
      { ...inspection.changes[0], suggestionId: 'unselected', proposedText: 'SQLite' },
    ] }
    const fetchMock = vi.fn().mockResolvedValueOnce(new Response(JSON.stringify(partial)))
      .mockResolvedValueOnce(new Response(new Blob(['synthetic-docx'])))
    vi.stubGlobal('fetch', fetchMock)
    vi.stubGlobal('URL', Object.assign(URL, { createObjectURL: vi.fn(() => 'blob:synthetic'), revokeObjectURL: vi.fn() }))
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {})
    render(<LayoutExportPanel optimizationId="plan-1" resumeId="resume-1" />)
    fireEvent.change(screen.getByLabelText('DOCX original'), { target: { files: [new File(['synthetic'], 'original.docx')] } })
    fireEvent.click(screen.getByRole('button', { name: 'Inspecionar DOCX' }))
    await screen.findByLabelText('Destino de PostgreSQL')
    fireEvent.change(screen.getByLabelText('Destino de PostgreSQL'), { target: { value: 'p:6' } })
    const download = screen.getByRole('button', { name: 'Baixar DOCX com layout original' })
    expect(download).toBeEnabled()
    expect(screen.getByText('Não incluídas neste download')).toBeVisible()
    fireEvent.click(download)
    await screen.findByText(/Download iniciado: 1 alteração\(ões\) incluída\(s\), 2 não incluída\(s\)/)
    expect(JSON.parse(fetchMock.mock.calls[1][1].body.get('placements'))).toEqual([
      { suggestionId: 'suggestion-1', blockId: 'p:6' },
    ])
  })
})
