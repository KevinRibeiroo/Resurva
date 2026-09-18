# DOCX Local Adaptation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Inspecionar DOCX reais e aplicar alterações aprovadas a uma cópia para revisão manual no Word.

**Architecture:** Ferramenta local existente, sem referências pela API. Inspeção e motor trabalham com bytes em memória; comandos cuidam do disco e relatórios. Toda edição é condicionada ao hash e às precondições de um plano.

**Tech Stack:** .NET 10, Open XML SDK 3.5.1, xUnit; nenhuma dependência nova.

**Spec:** `docs/superpowers/specs/2026-09-16-docx-original-adaptation-design.md`, aprovado em conversa em 2026-09-16.

## Global Constraints

- Não iniciar API, conectar banco, alterar frontend, contratos JSON da aplicação, Docker ou deploy.
- Não instalar LibreOffice. Saídas permanecem `visual_review_pending` para conferência no Word.
- Um tipo público por arquivo; modelos terminam em Model.
- Testes somente com dados fictícios. Documento pessoal só na verificação manual local, sem exportar seu texto no console.
- Continuar na branch de feature já escolhida pelo usuário, preservando mudanças existentes; sem commit/push automático.
- Os planos locais são sensíveis e devem ficar em diretório privado/ignorado. Não são autorizações autenticadas da API.

## Task 1: Inspeção e perfil explícito

**Files:** criar `DocxInspectionProfileModel.cs`, `DocxBlockModel.cs`, `DocxInspectionModel.cs`, `DocxDocumentInspector.cs`, `DocxReviewRequiredException.cs` em `tools/docx_layout_probe/`; testes próprios na pasta `tests/`.

**Interfaces:**

```csharp
public sealed record DocxInspectionProfileModel
{
    public string[] HeadingStyleIds { get; init; } = [];
    public Dictionary<string, string> BlockRoles { get; init; } = [];
}
public sealed record DocxBlockModel(string Id, int Index, string Text,
    string? StyleId, string? Section, string Kind, bool Editable);
public sealed record DocxInspectionModel(string SourceSha256, IReadOnlyList<DocxBlockModel> Blocks);
public static DocxInspectionModel Inspect(byte[] source, DocxInspectionProfileModel? profile = null);
```

`DocxDocumentInspector.Inspect` gera IDs `p:0`, `p:1` etc para parágrafos diretos, e SHA256 hexadecimal. Roles: `protected`, `section_heading`, `professional_title`, `summary`, `experience`, `skills`, `other`. Preamble protegido por padrão; `BlockRoles` pode identificar o título profissional, mas não liberar o primeiro parágrafo não vazio nem contatos. Resumo e habilidades inferidos por títulos portugueses/ingleses conhecidos; experiências apenas parágrafos de lista, nunca cabeçalhos de emprego/datas. Roles explícitos não liberam nome/contatos, títulos de seção ou campos/revisões.

- [x] Escrever testes que falhem para título customizado, herança de outline, fronteira de seção e proteção do preâmbulo.
- [x] Implementar resolução de estilos, detectando ciclo/referência ausente. `Heading1` continua aceito quando definido; `SectionHeader` exige perfil explícito se não tiver outline.
- [x] Suportar apenas corpo com parágrafos/seções; recusar tabelas, macro, assinaturas, controles/partes ativas e proteção documental. Parágrafos com conteúdo misto não editável continuam identificáveis, sem achatamento.
- [x] Executar `dotnet test tools/docx_layout_probe/tests/DocxLayoutProbe.Tests.csproj` e revisar.

Teste exemplificativo, expectativa independente do parser:

```csharp
var map = DocxDocumentInspector.Inspect(source,
    new DocxInspectionProfileModel { HeadingStyleIds = ["SectionHeader"] });
Assert.Equal("summary", map.Blocks.Single(b => b.Id == "p:2").Kind);
Assert.False(map.Blocks.Single(b => b.Id == "p:0").Editable);
```

## Task 2: Plano e motor transacional em memória

**Files:** criar `DocxAdaptationPlanModel.cs`, `DocxEditOperationModel.cs`, `DocxAdaptationResultModel.cs`, `DocxAdaptationEngine.cs`, `DocxPackageVerifier.cs`; modificar `DocxSkillInsertionProbe.cs` para reaproveitar a regra de inserção onde compatível; testes `DocxAdaptationEngineTests.cs`.

**Consumes:** Task 1.

**Produces:**

```csharp
public sealed record DocxEditOperationModel
{
    public string Id { get; init; } = "";
    public string BlockId { get; init; } = "";
    public string ExpectedText { get; init; } = "";
    public string Kind { get; init; } = "replace_text";
    public int Start { get; init; }
    public int Length { get; init; }
    public string NewText { get; init; } = "";
    public bool Approved { get; init; }
    public string[] EvidenceIds { get; init; } = [];
}
public sealed record DocxAdaptationPlanModel
{
    public int Version { get; init; } = 1;
    public string SourceSha256 { get; init; } = "";
    public DocxInspectionProfileModel Profile { get; init; } = new();
    public DocxEditOperationModel[] Operations { get; init; } = [];
}
public sealed record DocxAdaptationResultModel(byte[] Document,
    IReadOnlyList<string> AppliedOperationIds, string ReviewStatus, int ExistingSchemaErrors);
public static DocxAdaptationResultModel Apply(byte[] source, DocxAdaptationPlanModel plan);
```

- [x] Testes RED para substituições múltiplas, fragmentação em runs equivalentes e preservação de partes; referência de expectativa literal: `API em C#` → `API REST em C#` sem alterar empresa/data.
- [x] Validar versão/hash, IDs exclusivos, ExpectedText, aprovação/evidência declarada, destinos/roles e ranges. Recusar sobreposição; nenhuma saída parcial.
- [x] `replace_text` só em título profissional/resumo/itens de experiência. `insert_skill` só em categoria `skills` com dois pontos, reutilizando a montagem da lista do protótipo anterior. Evidência é referência declarada, não comprovação automática de fatos.
- [x] Preservar runs; substituição atravessando formatos diferentes é recusada. Processar ranges em ordem decrescente por bloco. Não alterar números em operações de experiência; cargos históricos e datas permanecem protegidos pela seleção dos blocos.
- [x] Comparar schema antes/depois por identificador/parte/caminho; comparar partes não alteradas byte a byte e XML fora dos nós de texto previstos semanticamente. Reabrir resultado e conferir texto esperado. Desabilitar AutoSave.
- [x] Testes de recusa: plano desatualizado, operações sem aprovação, overlaps, duplicatas, formatação mista, destino protegido, novos erros XML e preservação da entrada.

```csharp
var result = DocxAdaptationEngine.Apply(source, approvedPlan);
Assert.Equal("visual_review_pending", result.ReviewStatus);
Assert.Equal(originalSnapshot, source);
Assert.Throws<DocxReviewRequiredException>(() =>
    DocxAdaptationEngine.Apply(source, approvedPlan with { SourceSha256 = "invalid" }));
```

## Task 3: CLI e verificação real

**Files:** criar `DocxLocalCommands.cs`, `LocalArtifactWriter.cs`; modificar `Program.cs`; testes de comandos; atualizar documentação do protótipo.

**Consumes:** inspeção e Apply. **Produces:**

```text
inspect INPUT.docx PROFILE.json OUTPUT_DIRECTORY
apply INPUT.docx PLAN.json OUTPUT_DIRECTORY
```

`inspect` grava `inspection.json` e `plan.json` com operações vazias e perfil/hash. `apply` grava `adaptado.docx` e `report.json`; relatório não inclui texto pessoal. A CLI antiga de demonstração mantém o argumento único.

- [x] Escrever testes RED executando comandos contra diretórios temporários sintéticos: escrita de inspeção, plano válido, erro sem artefato, saída existente inalterada e console sem conteúdo do currículo.
- [x] Gravar resultados em pasta temporária irmã nova e mover para destino inexistente após conclusão; no erro, apagar somente o staging pertencente à operação. Usar `CreateNew`, nunca sobrescrever.
- [x] Implementar mensagens e códigos 0 (gerado, revisão pendente), 2 (falha/revisão). Não expor mensagens brutas de parsing com trechos pessoais.
- [x] Rodar suíte inteira e comandos reais; inspecionar o DOCX autorizado em diretório ignorado, sem incluí-lo nos testes ou commits.
- [x] Apresentar o mapa compatível. A redação proposta e a cópia pessoal ficam para a próxima etapa: requerem aprovação do conteúdo, não só da implementação.
- [x] Revisão independente de código e correção dos achados; atualizar documentação e reportar pendência visual. Não declarar integração à API.

## Registro de execução

- Ruling: manter a branch atual por escolha prévia do usuário; nenhum novo worktree ou commit é necessário para este escopo.
- Ruling: executar testes e revisão sem impor outra rodada de aprovação do plano; o usuário já aprovou a especificação e pediu implementação.
- Preflight: Tasks 1/2 compartilham somente contratos novos; Task 3 consome esses contratos. Alterações em Program e documentação pertencem à Task 3. Guardas do pacote e seleção de roles têm uma única autoridade no inspector.
- Estado inicial: 16 testes locais existentes; alterações anteriores do protótipo preservadas.
- Execução: implementação local após limite de uso do subagente inicial; os três grupos tiveram testes RED/GREEN. 53 testes DOCX aprovados ao final, sem avisos de compilação. Backend: 134 aprovados, 1 PostgreSQL ignorado; nenhuma API/banco externo foi iniciado.
- Revisão independente final disponível após retomada: pontuação podia mudar números sem tocar dígitos; a composição passa a validar os tokens do parágrafo inteiro. CLI agora identifica a operação recusada. Os dois achados tiveram reprodução RED, correção GREEN e revisão focada aprovada.
- Os scripts bash da skill não estavam disponíveis; registro/relatórios em `.artifacts/docx-local-adaptation/`, ignorado pelo Git, com revisão explícita dos arquivos não rastreados. Nenhum commit/push ou exclusão de trabalho anterior.
- Verificação real: inspeção aceita com perfil `SectionHeader`, 42 blocos; mapa/plano vazio privados. Sem geração de currículo pessoal adaptado nem validação visual. Demonstração, `inspect` e `apply` executados sobre dados fictícios; relatório `visual_review_pending`, zero erros de schema novos/preexistentes no exemplo.
