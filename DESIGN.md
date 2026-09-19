---
version: alpha
name: Resurva
description: Interface de comparação factual e adaptação de currículos, direção Acervo aprovada em 18/09/2026.
colors:
  background: '#edf1ec'
  surface: '#ffffff'
  text: '#1d332b'
  muted: '#526559'
  primary: '#285445'
  accent: '#dce6bf'
typography:
  sans:
    fontFamily: "'Public Sans', 'Segoe UI', sans-serif"
  mono:
    fontFamily: "ui-monospace, Consolas, monospace"
rounded:
  DEFAULT: '0.5rem'
  lg: '0.75rem'
spacing:
  field-gap: '1rem'
  section-gap: '2rem'
---

# Resurva — Acervo

## Overview

Produto em português para o candidato comparar evidências e revisar alterações, não um sistema de seleção. Direção aprovada: opção 1 verde da segunda rodada. Masthead verde floresta, fundo verde claro, tipografia editorial e folha branca de evidências. A análise prioriza a conferência de trechos; o score não representa chance de contratação.

Fonte canônica dos valores: `src/frontend/src/shared/styles/tokens.css`; este documento resume o sistema, não gera CSS. Componentes consomem os tokens diretamente. Não introduzir paleta independente por funcionalidade.

## Colors

Superfícies claras, texto verde escuro e ações em verde floresta. Alertas compartilhados distinguem erro, aviso e sucesso também por texto e ícone.

## Typography

Newsreader para marca e títulos; Public Sans para interface e evidências. Textos de currículo quebram linha; nunca truncar a única evidência disponível.

## Layout

Reutilizar a largura e o fluxo natural da página de adaptação. O painel de exportação ocupa a largura disponível; controles e ações se reorganizam no celular. No ambiente local, a ação principal DOCX do dock leva ao painel de layout original com foco de teclado. Modelo padrão permanece uma alternativa explícita. Preservar a navegação de outras telas.

## Elevation & Depth

Reutilizar `Card` e suas superfícies. Não acrescentar sombras ou animações ornamentais.

## Shapes

Campos usam os raios existentes. Ícones Material Symbols acompanham rótulos, não substituem instruções.

## Components

Proprietários canônicos: `shared/ui/Button`, `Card`, `Alert`, `Spinner`. Seleção de destino usa select nativo rotulado; seu popup é intencionalmente controlado pelo sistema operacional. Upload usa input de arquivo nativo acessível.

Estados do painel: sem arquivo, arquivo selecionado, inspeção, destinos disponíveis/bloqueados, exportação, erro recuperável e download iniciado. Não afirmar que a revisão visual foi concluída. Exportar o subconjunto com destinos válidos: habilitar com ao menos uma alteração selecionada e listar as não incluídas antes do download, com contagem após. Uma alteração sem destino não bloqueia as demais. Cancelar requisições obsoletas e preservar o arquivo após erro recuperável.

Originais permanecem apenas em memória, por sessão/usuário; não usar URLs nem armazenamento persistente do navegador para o conteúdo. A comparação, confirmação e exportação padrão continuam com seus contratos atuais.

## Do's and Don'ts

- Usar os componentes existentes e erros com orientação de recuperação.
- Distinguir DOCX baseado no original de PDF/DOCX em modelo padrão.
- Não prometer mesma paginação: revisão no Word ainda é necessária.
- Manter identidade Acervo entre landing, login, envio, resultado e adaptação; não adicionar serviços cloud para o redesign.

## Comparação e responsividade

`AnalysisResultView` apresenta o resultado; `EvidenceWorkbench` possui a seleção de requisitos. No desktop, evidências ficam na coluna direita; até 700px aparecem imediatamente abaixo do botão selecionado. Botões nativos mantêm Tab/Enter/Espaço, foco visível e `aria-expanded`. Ausência de evidência é explícita, sem inventar trechos. As cinco dimensões, pontos fortes, pontos de atenção e recomendações permanecem disponíveis.

Não mostrar cargo, nome do arquivo, currículo completo ou pesos fixos: o contrato de análise atual não fornece esses dados. O título genérico substitui o cargo fictício do mockup. Ações de adaptação preservam o fluxo de confirmação e os contratos existentes.

Grades usam colunas com mínimo zero; ações quebram linha e têm alvo mínimo de 44px. Abaixo de 600px, a barra de adaptação fica no fluxo da página para não cobrir o documento. O modo claro é o tema deste redesign. Respeitar movimento reduzido e manter barras de rolagem visíveis.

## Verificação local

A entrada `src/frontend/tests/visual/index.html` renderiza componentes reais com dados inteiramente fictícios, fora do bundle de produção. Com Vite em execução, abrir `/tests/visual/index.html` (resultado) ou `?upload` (envio). Não é um modo visitante nem altera autenticação; não usar dados pessoais nessa prévia.

## Canonical UI Map

| Capability | Canonical owner | Source of truth | Allowed variants | Verification |
| --- | --- | --- | --- | --- |
| Select/Listbox | Select nativo do LayoutExportPanel | Modelo de destinos retornado pela API | Popup nativo do sistema | layout-export.test.tsx |
| Form | ResumeDropzone e JobDescriptionInput | Limites e validação do fluxo de análise | Upload PDF/DOCX e descrição textual | newAnalysis.test.tsx |
| Scrollbar | shared/styles/reset.css | Tokens globais | Padrão CSS e fallback WebKit | Inspeção visual e DOM |
| Navigation | ButtonLink e React Router Link | Rotas existentes | Aparência primary, secondary e ghost | landing.test.tsx e testes de fluxo |
