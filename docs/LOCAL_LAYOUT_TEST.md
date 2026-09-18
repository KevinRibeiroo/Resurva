# Testar DOCX com layout original na aplicação local

Não precisa executar a CLI `inspect`/`apply`, instalar Python ou LibreOffice. A integração usa o motor .NET compartilhado. Não houve publicação no Cloud Run/Firebase.

## Iniciar

Com PostgreSQL/Firebase já configurados localmente, na raiz:

```powershell
dotnet run --project src/backend/ResumeMatcher.Api --launch-profile http
```

Em outro terminal, dentro de `src/frontend`:

```powershell
pnpm dev
```

Abra `http://localhost:5173`. API em `http://localhost:5080`, ambiente `Development`. `VITE_API_BASE_URL` local deve estar vazio para usar o proxy, não a URL publicada. Login continua obrigatório. A inicialização normal da API aplica migrations existentes; esta integração não acrescenta migration.

## Fluxo pela tela

1. Faça login, envie o **DOCX original** e compare com a vaga.
2. Entre em **Adaptar currículo**, revise/confirme as informações e clique em **Aplicar alterações aprovadas**.
3. Na barra de ações, clique em **DOCX — layout original**. Essa é a ação principal de exportação DOCX no ambiente local e leva ao painel **DOCX com layout original**, com foco de teclado. O último DOCX enviado fica em memória durante a sessão. Se recarregar ou abrir uma análise antiga, selecione novamente o original.
4. Clique em **Inspecionar DOCX**. Confira o destino de cada alteração: substituição com destino único já vem selecionada; inclusões sempre exigem escolha explícita.
5. Skills curtas podem entrar na categoria existente; frases completas podem ser acrescentadas ao fim de um resumo/item de experiência compatível. O texto é exatamente o aprovado (ou a declaração confirmada) no plano, sem uma nova chamada à IA.
6. Clique em **Baixar DOCX com layout original**. Abra no Word e revise linhas/páginas antes de enviar. Os botões **PDF — modelo padrão** e **DOCX — modelo padrão** continuam separados: não preservam layout.

Se uma mudança não tiver destino compatível, a tela mostra o motivo e não gera um documento parcial. Revise o plano ou use conscientemente a exportação padrão; não existe fallback silencioso.

## Contrato HTTP

- `POST /api/optimizations/{id}/layout/inspect`: multipart `file`. Retorno: `version`, `sourceSha256`, `changes` com `suggestionId`, `proposedText`, `kind`, `candidates` (`id`, `text`, `section`), `blockedReason` e `reviewStatus: visual_review_pending`.
- `POST /api/optimizations/{id}/layout/export`: multipart `file`, `version`, `sourceSha256`, `placements` (JSON de pares `suggestionId`/`blockId`). Retorna DOCX binário com `Cache-Control: no-store`.
- JWT, conta autorizada, ownership, rate limiting e retenção existentes permanecem. Ambos retornam 404 fora de `Development`; o painel só aparece com Vite em desenvolvimento.
- A API usa apenas decisões/aplicações persistidas e confirmações canônicas. O cliente não pode enviar texto de substituição ou flags de aprovação.
- Arquivo diferente ou versão/hash obsoletos: 409. DOCX inválido, layout incompatível ou destino inválido: 400. Arquivo/plano indisponível ao usuário: 404. Ausência de login: 401.

## Privacidade e limites

- O original não é salvo no banco/storage. Nos novos endpoints de layout, o multipart é limitado e configurado para permanecer em memória, sem spill em arquivo temporário. Esses endpoints não gravam documentos em disco nem renovam retenção por exportar. Isso não altera o buffering do endpoint de upload já existente.
- O navegador guarda no máximo o último original em memória, vinculado à sessão. Logout/troca de usuário invalidam inclusive uploads pendentes. Não usa localStorage, sessionStorage ou URLs para o conteúdo.
- Arquivo e pacote descompactado: até 10 MiB. Há 64 KiB adicionais apenas para envelope multipart, não para ampliar o tamanho do arquivo. Pacotes ativos/protegidos e estruturas incompatíveis são recusados.
- O texto extraído deve coincidir com `OriginalText`, normalizando apenas espaços. SHA-256 vincula inspeção/exportação. Como o upload anterior não guarda o binário/hash, isso não comprova identidade byte a byte com o primeiro upload: uma variante de layout com texto idêntico pode ser reselecionada.
- Suporte: uma coluna, parágrafos/runs simples, títulos por outline/Heading1/SectionHeader, resumo, itens de experiência e skills categorizadas. Datas, identidade e cabeçalhos de emprego ficam protegidos. O adaptador HTTP reconhece títulos profissionais simples de desenvolvimento/engenharia de software (português/inglês), antes da primeira seção e após o nome. A substituição ainda exige trecho exato, aprovação no plano e formatação compatível; parágrafos com contatos ou anos permanecem protegidos.
- Substituições sobrepostas, tabelas, múltiplas colunas e formatação ambígua são recusadas. Inclusões no mesmo bloco são sequenciais. Falta de evidência não bloqueia sozinha uma inclusão confirmada, mas os limites estruturais continuam valendo.
- Preservação estrutural não garante paginação: sem reflow, renderização/PDF fiel, redução de fonte ou truncamento automático. A revisão visual no Word permanece obrigatória.

## Arquitetura e validação

Application define `ILayoutPreservingExportService` e modelos próprios. Infrastructure coordena plano canônico e documento. `ResumeMatcher.DocumentLayout` contém somente edição/inspeção/verificação Open XML; Domain/Application não dependem de Open XML. API não referencia o executável da CLI.

Testes sintéticos HTTP cobrem geração, confirmação, ownership, expiração, hash/versão, adulteração, inclusão múltipla e indisponibilidade em produção. Testes frontend cobrem inspeção, destinos, download, recuperação e isolamento da sessão. A suíte própria do motor continua em `tools/docx_layout_probe/tests`.

### Verificação em 2026-09-17

- Backend: 148 testes aprovados; 1 teste PostgreSQL externo não executado, para não acessar banco real.
- Motor DOCX: 53 testes aprovados.
- Frontend: TypeScript/build aprovados e 26 testes aprovados.
- Navegador Chrome headless: página de adaptação com plano fictício, recuperação de erro, seleção explícita/teclado, download, movimento reduzido e largura de 390 px. A borda HTTP foi simulada nesse teste visual; a geração real de DOCX foi verificada nos testes HTTP com banco em memória, não numa sessão Firebase real.
- Revisão independente dos ajustes de multipart em memória, isolamento por sessão e dock: sem bloqueios restantes.
- Auditoria estática focada nos novos componentes/serviços: sem achados; lint de `DESIGN.md`: sem erros. A auditoria global também apontou controles/textarea/scrollbars preexistentes, fora deste escopo, além de falsos positivos em botões dentro de links; não equivale a uma auditoria completa de UX aprovada.
- Não houve teste visual do DOCX renderizado no Word, publicação, migração ou execução da API contra o Supabase.
