---
version: alpha
name: Resurva
description: Interface de comparação factual e adaptação de currículos, preservando o visual atual.
colors:
  background: '#0b0d13'
  surface: '#1a1f2c'
  text: '#f8fafc'
  muted: '#94a3b8'
  primary: '#6366f1'
  accent: '#4cd7f6'
typography:
  sans:
    fontFamily: "'Plus Jakarta Sans', 'Segoe UI', sans-serif"
  mono:
    fontFamily: "'JetBrains Mono', monospace"
rounded:
  DEFAULT: '0.5rem'
  lg: '0.75rem'
spacing:
  field-gap: '1rem'
  section-gap: '2rem'
---

# Resurva — contexto visual existente

## Overview

Produto em português para o candidato comparar evidências e revisar alterações, não um sistema de seleção. A referência é a tela atual de adaptação: documento original e decisões lado a lado, cartões escuros e acentos violeta/ciano. Esta integração não redesenha o produto.

Fonte canônica dos valores: `src/frontend/src/shared/styles/tokens.css`; este documento resume o sistema, não gera CSS. Componentes consomem os tokens diretamente. Não introduzir paleta independente por funcionalidade.

## Colors

Superfícies escuras, texto claro e contraste de hierarquia. Ciano/violeta para ações; alertas compartilhados distinguem erro, aviso e sucesso também por texto e ícone.

## Typography

Preservar as famílias e classes tipográficas existentes. Textos de currículo quebram linha; nunca truncar a única evidência disponível.

## Layout

Reutilizar a largura e o fluxo natural da página de adaptação. O painel de exportação ocupa a largura disponível; controles e ações se reorganizam no celular. No ambiente local, a ação principal DOCX do dock leva ao painel de layout original com foco de teclado. Modelo padrão permanece uma alternativa explícita. Preservar a navegação de outras telas.

## Elevation & Depth

Reutilizar `Card` e suas superfícies. Não acrescentar sombras ou animações ornamentais.

## Shapes

Campos usam os raios existentes. Ícones Material Symbols acompanham rótulos, não substituem instruções.

## Components

Proprietários canônicos: `shared/ui/Button`, `Card`, `Alert`, `Spinner`. Seleção de destino usa select nativo rotulado; seu popup é intencionalmente controlado pelo sistema operacional. Upload usa input de arquivo nativo acessível.

Estados do painel: sem arquivo, arquivo selecionado, inspeção, destinos disponíveis/bloqueados, exportação, erro recuperável e download iniciado. Não afirmar que a revisão visual foi concluída. Desabilitar exportação enquanto houver destino faltando; cancelar requisições obsoletas e preservar o arquivo após erro recuperável.

Originais permanecem apenas em memória, por sessão/usuário; não usar URLs nem armazenamento persistente do navegador para o conteúdo. A comparação, confirmação e exportação padrão continuam com seus contratos atuais.

## Do's and Don'ts

- Usar os componentes existentes e erros com orientação de recuperação.
- Distinguir DOCX baseado no original de PDF/DOCX em modelo padrão.
- Não prometer mesma paginação: revisão no Word ainda é necessária.
- Não redesenhar outras telas ou adicionar serviços cloud nesta integração local.
