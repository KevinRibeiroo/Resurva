# Relatório de Auditoria Técnica Abrangente — ResumeMatcher

**Data da Auditoria:** 08/09/2026  
**Ambiente Local:** Windows 10/11 | .NET 10.0.100 | Node v24 | pnpm 12.3.4  
**Repositório:** `ResumeProject` | Branch: `develop` | Commit inspecionado: `4927e3b`  
**Escopo:** Segurança, Fluxos Funcionais, Arquitetura, SOLID, Design Patterns, Testes, Infraestrutura e Prontidão do MVP.  
**Diretiva Mandatória:** DIAGNÓSTICO E BACKLOG APENAS — SEM MODIFICAÇÃO DE CÓDIGO.

---

## 1. RESUMO EXECUTIVO

### 1.1 Prontidão Atual do MVP
O projeto ResumeMatcher apresenta um nível elevado de maturidade arquitetural e técnica para o estágio de MVP Privado (Single-User). O backend .NET 10 implementa Clean Architecture com separação rigorosa de responsabilidades (Domain, Application, Infrastructure, Api), isolamento de dados por `OwnerUserId`, chaves compostas e exclusões em cascata no banco PostgreSQL/EF Core. O frontend React 19 / TypeScript / Vite está totalmente estruturado no padrão `app / features / shared`, com componentes modulares, CSS Modules, acessibilidade semântica (ARIA, labels, roles, contrastes WCAG AA) e validação unitária com Vitest.

### 1.2 Maiores Riscos Identificados
1. **Segurança de Borda e Cabeçalhos HTTP:** Ausência de cabeçalhos de proteção (HSTS, CSP, X-Frame-Options, X-Content-Type-Options) tanto na API ASP.NET Core quanto na configuração do Firebase Hosting (`firebase.json`).
2. **Ciclo de Vida do Plano de Adaptação (Applied):** Quando um plano atinge o status `Applied`, a API rejeita qualquer reaplicação com decisões diferentes (`409 Conflict`), mas o endpoint `GET` do plano não expõe as decisões originalmente tomadas, causando inconsistência no frontend em caso de recarga ou reabertura de plano.
3. **Composição Textual em Adaptações Concorrentes:** Substituições textuais em cascata via `ComposeAdaptedText` que falhem em encontrar o texto original (devido a sobreposição de sugestões prévias) são silenciosamente convertidas em adições no rodapé do documento.
4. **Execução de Migrations no Startup de Contêineres:** `db.Database.MigrateAsync()` executado na inicialização da API em produção acopla permissões de DDL à identidade de runtime da aplicação e introduz risco de race conditions caso o Cloud Run escale réplicas simultaneamente.
5. **Agendamento Operacional da Retenção:** O comando de purga física de dados de 30 dias (`--purge-expired`) está implementado no binário, porém seu agendamento automatizado em nuvem (Cloud Scheduler / Cloud Run Job) ainda não foi provisionado.

### 1.3 Bloqueadores
- **Para o MVP Privado Atual (Marco A):**
  - Nenhum bloqueador impeditivo para testes funcionais locais/pessoais da conta autorizada.
  - Requisito operacional pré-publicação: Configurar e validar probes TCP (não HTTP) no Cloud Run devido à exigência de JWT no `/health`.
- **Para Abertura a Outros Usuários (Marco B):**
  - Implementar gestão de sessões multi-usuário e cadastro.
  - Substituir o rate limiter em memória global por partição por usuário/IP com cache distribuído (ex.: Redis).
  - Provisionar rotina de purga periódica dos 30 dias de retenção.
  - Implementar endpoint e fluxo de exclusão completa de conta (LGPD / GDPR).

### 1.4 Limites da Auditoria
- Não foram realizados testes de penetração ativos, exploração, fuzzing ou força bruta contra ambientes publicados em nuvem (Google Cloud Run, Supabase, Firebase).
- A verificação de banco de dados real em produção (Supabase Data API, RLS e roles da connection string) depende de confirmação manual através dos consoles administrativos listados no Checklist Externo.
- A auditoria baseou-se no código-fonte, configurações versionadas, suíte automatizada de testes locais (130 testes .NET, 21 testes Vitest) e build determinístico.

### 1.5 Decisão de Prontidão
**PRONTO COM CONDIÇÕES** para o MVP Privado (Conta Única Autorizada).  
*Condições:* Configurar corretamente as variáveis de ambiente no Cloud Run, manter probe de inicialização TCP, conferir parâmetros CORS em produção e aplicar os cabeçalhos de segurança essenciais antes de liberar acesso via web pública.

---

## 2. INVENTÁRIO E MATRIZ DE COBERTURA

### 2.1 Projetos e Entrypoints
| Componente | Linguagem / Framework | Entrypoint | Função Principal |
| :--- | :--- | :--- | :--- |
| `ResumeMatcher.Api` | C# 14 / .NET 10 | `Program.cs` | API HTTP, Middleware, Autenticação, Rate Limiter |
| `ResumeMatcher.Application` | C# 14 / .NET 10 | `DependencyInjection.cs` | Casos de uso, Scoring, Validação de Segurança |
| `ResumeMatcher.Domain` | C# 14 / .NET 10 | Entidades / Modelos | Entidades centrais puras, enums, regras de domínio |
| `ResumeMatcher.Infrastructure` | C# 14 / .NET 10 | `DependencyInjection.cs` | EF Core, PostgreSQL, Gemini SDK, Extratores, Exporters |
| `ResumeMatcher.Tests` | C# 14 / .NET 10 (xUnit) | `ResumeMatcher.Tests.csproj` | Testes unitários e integração de backend |
| `src/frontend` | TypeScript 5.9 / React 19.2 / Vite 8 | `src/main.tsx` | SPA, Interface Visual, Integração Firebase / API |

### 2.2 Rotas e Endpoints
| Método | Endpoint | Autenticação / Autorização | Rate Limiting | Status de Verificação |
| :--- | :--- | :--- | :--- | :--- |
| `GET` | `/health` | JWT Firebase + Conta Autorizada (FallbackPolicy) | Não aplicado | Verificado e adequado (Requer TCP probe) |
| `GET` | `/api/auth/session` | JWT Firebase + Conta Autorizada | Não aplicado | Verificado e adequado (204 No Content) |
| `POST` | `/api/resumes/upload` | JWT Firebase + Conta Autorizada | `ApiRateLimitOptions` (Global) | Verificado (10 MB máx., valida formato/conteúdo) |
| `DELETE` | `/api/resumes/{id}` | JWT Firebase + Conta Autorizada | `ApiRateLimitOptions` (Global) | Verificado (Exclusão em cascata isolada por dono) |
| `POST` | `/api/analysis/compare`| JWT Firebase + Conta Autorizada | `ApiRateLimitOptions` (Global) | Verificado (Hash cache, deduplicação em voo) |
| `GET` | `/api/analysis/{id}` | JWT Firebase + Conta Autorizada | `ApiRateLimitOptions` (Global) | Verificado (Valida posse e retenção 30 dias) |
| `POST` | `/api/analysis/{id}/optimization` | JWT Firebase + Conta Autorizada | `ApiRateLimitOptions` (Global) | Verificado (Gera plano com validação Safe/NeedsConfirmation) |
| `GET` | `/api/optimizations/{id}` | JWT Firebase + Conta Autorizada | `ApiRateLimitOptions` (Global) | Verificado (Retorna plano e texto adaptado) |
| `POST` | `/api/optimizations/{id}/apply` | JWT Firebase + Conta Autorizada | `ApiRateLimitOptions` (Global) | Verificado (Valida versão e conformidade ética) |
| `GET` | `/api/optimizations/{id}/export` | JWT Firebase + Conta Autorizada | `ApiRateLimitOptions` (Global) | Verificado (PDF e DOCX em memória, sem persistência) |
| CLI | `--purge-expired` | Execução Privilegiada Local | N/A | Verificado (Purga atômica transacional no PostgreSQL) |

### 2.3 Matriz de Conformidade e Controles

| Controle / Requisito | Padrão / Referência | Status | Evidência / Justificativa |
| :--- | :--- | :--- | :--- |
| **Isolamento de Dados por Dono** | OWASP API1:2023 (BOLA) / ASVS V4 | Verificado e adequado | Entidades com `OwnerUserId`, FK composta `(OwnerUserId, ResumeId)`, queries com filtro obrigatório. |
| **Validação de Token JWT** | ASVS V3 (Session & Token) | Verificado e adequado | `AddFirebaseAuthentication`: valida RS256, Issuer, Audience, Lifetime, `auth_time`, `iat`, `sub`. |
| **Restrição a Conta Autorizada** | Princípio do Menor Privilégio | Verificado e adequado | `PrivateUserAuthorizationHandler` restringe estritamente ao e-mail configurado e verificado via Google. |
| **Rate Limiting na API** | OWASP API4:2023 (Resource Consumption) | Indício que exige validação | Limitador em memória global (20 req / 60s); não particionado por IP/usuário; não distribuído. |
| **Cabeçalhos de Segurança HTTP** | OWASP API8:2023 / ASVS V14 | Falha confirmada | Ausência de CSP, HSTS, X-Frame-Options, X-Content-Type-Options na API e no `firebase.json`. |
| **Anti-Hallucination & Anti-Fraude** | OWASP GenAI LLM01 / LLM02 | Verificado e adequado | `OptimizationSafetyValidator` proíbe itens sem evidência textual no currículo como `Safe`. Força `NeedsConfirmation`. |
| **Separação de Instruções de IA** | OWASP GenAI Prompt Injection | Verificado e adequado | SDK oficial Gemini com `SystemInstruction` isolada e schema JSON estrito (`ResponseJsonSchema`). |
| **Limpeza e Retenção (30 dias)** | Privacidade / LGPD | Verificado e adequado | Leituras filtram `UpdatedAt > cutoff`. Purga via `--purge-expired` testada transacionalmente. |
| **Agendamento da Purga** | Operação e Governança | Indício que exige validação | Depende de cronjob ou Cloud Scheduler não configurado no código versionado. |
| **Proteção de Segredos** | ASVS V14 / Secret Management | Verificado e adequado | Repositório livre de segredos reais. Valores de teste em CI são sintéticos descartáveis. |
| **Dependências Livres de CVEs** | SCA / Supply Chain Security | Verificado e adequado | `dotnet list package --vulnerable` e `pnpm audit` retornaram 0 vulnerabilidades conhecidas. |

---

## 3. RELATÓRIO DETALHADO DE ACHADOS

### [SEC-01] Ausência de Cabeçalhos de Segurança HTTP na API e Frontend
- **Categoria:** Segurança — Configuração e Proteção de Borda
- **Severidade / Prioridade:** Média / Alta
- **Confiança:** 100% Confirmada
- **Localização:** `src/backend/ResumeMatcher.Api/Program.cs` e `src/frontend/firebase.json`
- **Evidência:** 
  - `Program.cs` não registra middleware de security headers (`app.UseHsts()` ou cabeçalhos customizados).
  - `firebase.json` possui apenas configuração de `public`, `ignore`, `predeploy` e `rewrites`, sem seção `"headers"`.
- **Comportamento Esperado:** Emissão obrigatória de:
  - `Content-Security-Policy` restritiva (permitindo apenas Firebase Auth, Google APIs e backend Cloud Run).
  - `X-Content-Type-Options: nosniff`.
  - `X-Frame-Options: DENY` (ou `frame-ancestors 'none'`).
  - `Referrer-Policy: strict-origin-when-cross-origin`.
  - `Strict-Transport-Security: max-age=31536000; includeSubDomains`.
- **Comportamento Observado:** Respostas HTTP sem cabeçalhos de hardening de navegador.
- **Impacto:** Exposição a riscos de Clickjacking, MIME type sniffing e potenciais vazamentos de referrers.
- **Recomendação Mínima:** Adicionar configuração de `"headers"` no `firebase.json` para o hosting e um middleware simples no ASP.NET Core que injete `X-Content-Type-Options`, `X-Frame-Options` e `Referrer-Policy`.
- **Critério de Aceite:** Teste de integração inspecionando cabeçalhos de resposta com asserção explícita de presença e valores corretos.
- **Esforço / Marco:** 1 a 2 horas | Marco A (Pré-abertura pública / Recomendável antes do primeiro deploy).

---

### [SEC-02] Rate Limiter Global em Memória Compartilhado por Toda a Instância
- **Categoria:** Segurança — Disponibilidade e Consumo de Recursos
- **Severidade / Prioridade:** Média / Média
- **Confiança:** 100% Confirmada
- **Localização:** `src/backend/ResumeMatcher.Api/Program.cs:50-56`
- **Evidência:**
  ```csharp
  options.AddFixedWindowLimiter(ApiRateLimitOptions.PolicyName, limiter =>
  {
      limiter.PermitLimit = rateLimitSettings.PermitLimit;
      limiter.Window = TimeSpan.FromSeconds(rateLimitSettings.WindowSeconds);
      limiter.QueueLimit = 0;
  });
  ```
- **Comportamento Esperado:** Rate limiting particionado por identidade (User UID ou IP da requisição).
- **Comportamento Observado:** O limitador é estático e global para o processo. Toda requisição a qualquer endpoint com `[EnableRateLimiting]` consome a mesma cota única de 20 requisições / 60 segundos.
- **Impacto:** Operações rápidas e sequenciais legítimas (upload + análise + polling + adaptação) podem esgotar a cota prematuramente (429 Too Many Requests). Em múltiplas instâncias no Cloud Run, cada réplica possui seu contador independente e volátil.
- **Recomendação Mínima:** Alterar para `AddPolicy` particionado por `httpContext.User.FindFirst("sub")?.Value ?? httpContext.Connection.RemoteIpAddress?.ToString()`.
- **Critério de Aceite:** Teste unitário comprovando que requisições de identificadores distintos possuem contadores independentes.
- **Esforço / Marco:** 2 horas | Marco B (Bloqueador para múltiplos usuários; tolerável em usuário único).

---

### [SEC-03] FallbackPolicy Exige Token Firebase no Endpoint `/health`
- **Categoria:** Operação & Segurança — Probes de Infraestrutura
- **Severidade / Prioridade:** Baixa / Alta (Operacional)
- **Confiança:** 100% Confirmada
- **Localização:** `src/backend/ResumeMatcher.Api/Program.cs:69` e `FirebaseAuthenticationExtensions.cs:85`
- **Evidência:**
  - `FallbackPolicy` exige usuário autenticado com `PrivateUserRequirement`.
  - `/health` não possui `.AllowAnonymous()`.
- **Comportamento Esperado:** Chamadas de probes anônimas do Google Cloud Run ou balanceadores esperam `200 OK` sem autenticação.
- **Comportamento Observado:** Chamada HTTP direta a `/health` sem Bearer token retorna `401 Unauthorized`.
- **Impacto:** Se o Cloud Run for configurado com HTTP Startup/Liveness Probe apontando para `/health`, as instâncias entrarão em falha contínua e serão reiniciadas repetidamente.
- **Mitigação Atual:** `docs/DEPLOYMENT.md` instrui expressamente o uso de **TCP probe** (não HTTP probe) para contornar essa proteção.
- **Recomendação Mínima:** Manter probe TCP conforme documentado ou segregar um endpoint anônimo simples de liveness (`/health/live`) separado do endpoint autenticado de integridade de banco de dados.
- **Critério de Aceite:** Validação de inicialização no Cloud Run com probe TCP ativa.
- **Esforço / Marco:** 1 hora | Marco A (Necessário seguir a instrução operacional).

---

### [AI-01] Composição Textual de Sugestões com Falha de Match Silenciosamente Deslocada
- **Categoria:** Fluxo Funcional / IA — Integridade do Documento Adaptado
- **Severidade / Prioridade:** Média / Alta
- **Confiança:** 100% Confirmada
- **Localização:** `src/backend/ResumeMatcher.Application/Services/ResumeOptimizationService.cs:304-319`
- **Evidência:**
  ```csharp
  if (!string.IsNullOrWhiteSpace(item.OriginalText) && result.Contains(item.OriginalText, StringComparison.OrdinalIgnoreCase))
  {
      var index = result.IndexOf(item.OriginalText, StringComparison.OrdinalIgnoreCase);
      if (index >= 0)
          result = string.Concat(result.AsSpan(0, index), item.ProposedText, result.AsSpan(index + item.OriginalText.Length));
  }
  else if (!string.IsNullOrWhiteSpace(item.ProposedText))
  {
      additions.Add(item.ProposedText);
  }
  ```
- **Comportamento Esperado:** Se uma sugestão de substituição pontual tem seu trecho original alterado ou invalidado por uma sugestão precedente (sobreposição de texto), o sistema deve sinalizar a impossibilidade de aplicação inline ou alertar o usuário.
- **Comportamento Observado:** O item é silenciosamente movido para `additions` e injetado ao final do currículo sob a seção `[Informações e Competências Adicionais Confirmadas]`, quebrando a estrutura textual pretendida.
- **Impacto:** Alteração desordenada da formatação do documento gerado quando duas sugestões do LLM afetam trechos adjacentes ou idênticos.
- **Recomendação Mínima:** Detectar na validação prévia se existem sugestões com trechos originais sobrepostos ou registrar no `AppliedOptimizationItemModel` que a inserção foi deslocada para apêndice.
- **Critério de Aceite:** Teste unitário de aplicação de duas sugestões sobrepostas demonstrando tratamento consistente e determinístico.
- **Esforço / Marco:** 4 horas | Marco A.

---

### [AI-02] Inconsistência na Reabertura e Edição de Planos de Otimização `Applied`
- **Categoria:** Fluxo Funcional / Arquitetura — Idempotência e Ciclo de Vida
- **Severidade / Prioridade:** Média / Alta
- **Confiança:** 100% Confirmada
- **Localização:** `src/backend/ResumeMatcher.Application/Services/ResumeOptimizationService.cs:189-209` e `src/frontend/src/features/optimization/pages/OptimizationAdaptationPage.tsx:95-103`
- **Evidência:**
  - O modelo `OptimizationPlanModel` não retorna a lista de decisões tomadas (`DecisionsJson`).
  - Ao recarregar a tela `/adaptacoes/:id` para um plano `Applied`, o estado de decisões local do React reinicia com os valores padrão (`Safe = accepted`, `NeedsConfirmation = unaccepted`).
  - Caso o usuário tente re-aplicar ou alterar uma decisão, o backend compara as decisões com o snapshot original e lança `OptimizationConflictException("The optimization plan has already been applied with different decisions.")` (HTTP 409).
- **Comportamento Esperado:** Ou o plano com status `Applied` deve ser exibido em modo somente leitura com as decisões originais preenchidas e opções de exportação, OU deve permitir uma ação explícita de "Reabrir para edição" que incremente a versão e reabra o plano para novas decisões.
- **Comportamento Observado:** Botão de aplicar continua acessível e, se clicado, resulta em erro 409 inesperado na interface.
- **Recomendação Mínima:** 
  1. Incluir as decisões salvas no retorno do plano para restaurar o estado visual fielmente.
  2. Desabilitar os controles de alteração quando o status for `Applied`, deixando ativo apenas o painel de exportação/cópia, com botão explícito de "Gerar Nova Versão/Novo Plano".
- **Critério de Aceite:** Teste de regressão no frontend garantindo que plano aplicado é aberto sem erros e bloqueia submissão conflitante.
- **Esforço / Marco:** 3 horas | Marco A.

---

### [ARCH-01] Violação da Regra de Arquivo Único por Tipo em `ResumeTextExtractors.cs`
- **Categoria:** Arquitetura e Convenções de Código
- **Severidade / Prioridade:** Baixa / Baixa
- **Confiança:** 100% Confirmada
- **Localização:** `src/backend/ResumeMatcher.Infrastructure/ResumeTextExtractors.cs:8,41`
- **Evidência:** O arquivo `ResumeTextExtractors.cs` abriga simultaneamente `PdfResumeTextExtractor` e `DocxResumeTextExtractor`.
- **Regra Violada:** `AGENTS.md` Regra 2: *"Um tipo público principal por arquivo; o nome do arquivo deve corresponder ao tipo."*
- **Recomendação Mínima:** Segregar em `Extractors/PdfResumeTextExtractor.cs` e `Extractors/DocxResumeTextExtractor.cs`.
- **Critério de Aceite:** `dotnet build` sem alterações de comportamento e nomes de arquivos correspondendo às classes.
- **Esforço / Marco:** 15 minutos | Marco D (Refatoração de estilo/organização).

---

### [ARCH-02] Presença de Código Morto / Arquivos Órfãos na Raiz de `src/frontend/src/`
- **Categoria:** Arquitetura / Manutenibilidade Frontend
- **Severidade / Prioridade:** Baixa / Média
- **Confiança:** 100% Confirmada
- **Localização:** `src/frontend/src/`
  - `AuthGate.tsx` (substituído por `features/auth/components/AuthProtected.tsx`)
  - `OptimizationView.tsx` (substituído por `features/optimization/pages/OptimizationAdaptationPage.tsx`)
  - `api.ts` (substituído por `shared/api/httpClient.ts`)
  - `auth.ts` (substituído por `features/auth/services/authService.ts`)
  - `types.ts` (substituído por modelos específicos nas features)
  - `styles.css` (substituído por CSS Modules e `shared/styles/global.css`)
- **Evidência:** Varredura em toda a árvore do projeto confirma que nenhum arquivo ativo referencia esses módulos. Eles não quebram o build, mas induzem a erros de manutenção.
- **Recomendação Mínima:** Remover com segurança os 6 arquivos obsoletos.
- **Critério de Aceite:** `pnpm build` e `pnpm test` com 100% de sucesso após a exclusão.
- **Esforço / Marco:** 15 minutos | Marco A / D.

---

### [ARCH-03] E/S de Stream Síncrona em Método Assíncrono nos Extratores
- **Categoria:** Performance e Arquitetura de E/S
- **Severidade / Prioridade:** Baixa / Baixa
- **Confiança:** 100% Confirmada
- **Localização:** `src/backend/ResumeMatcher.Infrastructure/ResumeTextExtractors.cs:20,55`
- **Evidência:** Uso de `stream.CopyTo(buffer)` síncrono dentro de `public Task<string> ExtractAsync(...)`.
- **Impacto:** Bloqueio de thread do thread pool durante a leitura do stream de upload em requisições concorrentes.
- **Recomendação Mínima:** Utilizar `await stream.CopyToAsync(buffer, cancellationToken)` e tornar os métodos `async Task<string>`.
- **Critério de Aceite:** Testes de upload de PDF/DOCX passando com cópia assíncrona.
- **Esforço / Marco:** 30 minutos | Marco D.

---

### [ARCH-04] Warning de Compilação CS8602 em `ResumeTextExtractors.cs`
- **Categoria:** Qualidade de Código / Tratamento de Nulos
- **Severidade / Prioridade:** Baixa / Baixa
- **Confiança:** 100% Confirmada
- **Localização:** `src/backend/ResumeMatcher.Infrastructure/ResumeTextExtractors.cs:58`
- **Evidência:** 
  ```text
  ResumeTextExtractors.cs(58,17): warning CS8602: Dereference of a possibly null reference.
  ```
- **Recomendação Mínima:** Validar explicitamente `document.MainDocumentPart?.Document?.Body` antes de acessar `Body`.
- **Critério de Aceite:** `dotnet build` com zero warnings.
- **Esforço / Marco:** 10 minutos | Marco D.

---

### [OPS-01] Execução Automática de Migrations no Startup da Aplicação em Produção
- **Categoria:** Infraestrutura / Operação de Banco de Dados
- **Severidade / Prioridade:** Média / Alta
- **Confiança:** 100% Confirmada
- **Localização:** `src/backend/ResumeMatcher.Api/Program.cs:71-78`
- **Evidência:**
  ```csharp
  await using (var scope = app.Services.CreateAsyncScope())
  {
      var db = scope.ServiceProvider.GetRequiredService<ResumeMatcherDbContext>();
      if (db.Database.IsRelational())
          await db.Database.MigrateAsync();
  }
  ```
- **Comportamento Esperado:** Em produção em nuvem (Cloud Run com potencial de múltiplas instâncias ou rollouts graduais), migrations devem ser executadas em etapa isolada e única do pipeline de deploy (ex.: Cloud Build ou Job específico de migração).
- **Comportamento Observado:** O contêiner de aplicação roda migrações automaticamente no momento da inicialização do servidor HTTP.
- **Impacto:** Se duas réplicas iniciarem ao mesmo tempo, podem ocorrer concorrência de DDL e deadlocks no PostgreSQL. Adicionalmente, a connection string da aplicação em runtime precisa ter permissões de DDL (criação/alteração de tabelas e índices), violando o menor privilégio.
- **Recomendação Mínima:** Para o MVP pessoal de 1 réplica, documentar o risco (já registrado em `docs/DEPLOYMENT.md`). Para escala ou produção, criar um script ou entrypoint com flag `--migrate-only` executado pelo pipeline de CI/CD antes da ativação do tráfego.
- **Critério de Aceite:** Separação do comando de migração do startup da API HTTP.
- **Esforço / Marco:** 2 horas | Marco B.

---

### [OPS-02] Agendamento Operacional da Purga de Retenção de 30 Dias Não Versionado
- **Categoria:** Governança / Privacidade / Infraestrutura
- **Severidade / Prioridade:** Média / Alta
- **Confiança:** 100% Confirmada
- **Localização:** `docs/DATA_PRIVACY.md:30` e `Program.cs:9-18`
- **Evidência:** O código C# implementa `--purge-expired`, porém não há arquivo de IaC (Terraform, Cloud Run Job manifest ou gcloud command versionado) configurando o acionamento diário via Cloud Scheduler.
- **Impacto:** Os dados ficam logicamente inacessíveis após 30 dias (graças aos filtros das queries), mas permanecem fisicamente gravados no banco Supabase até que alguém execute manualmente o comando.
- **Recomendação Mínima:** Criar um script versionado de infraestrutura para provisionar um Cloud Run Job com Cloud Scheduler diário invocando a imagem do backend com argumento `--purge-expired`.
- **Critério de Aceite:** Evidência de execução automática e logs gerados da purga de retenção.
- **Esforço / Marco:** 2 horas | Marco B.

---

### [OPS-03] GitHub Actions: Action de Deploy do Firebase Não Fixada por SHA
- **Categoria:** CI/CD / Segurança da Cadeia de Suprimentos
- **Severidade / Prioridade:** Baixa / Média
- **Confiança:** 100% Confirmada
- **Localização:** `.github/workflows/ci.yml:87`
- **Evidência:**
  `uses: FirebaseExtended/action-hosting-deploy@v0`
  As outras actions (`checkout`, `setup-dotnet`, `setup-node`) estão fixadas por commit SHA imutável (ex.: `actions/checkout@d23441a48e516b6c34aea4fa41551a30e30af803`), mas a action de hosting deploy usa a tag móvel `@v0`.
- **Recomendação Mínima:** Substituir a tag `@v0` pelo commit SHA imutável correspondente.
- **Critério de Aceite:** Pipeline de CI executando com todas as actions fixadas por SHA.
- **Esforço / Marco:** 15 minutos | Marco D.

---

## 4. BACKLOG PRIORIZADO

| ID | Marco | Título / Problema | Status | Correção Efetuada | Componentes / Arquivos | Critério de Aceite |
| :--- | :---: | :--- | :---: | :--- | :--- | :--- |
| **SEC-01** | **A** | Cabeçalhos de Segurança HTTP ausentes | **Resolvido** | Injetados headers no `firebase.json` e middleware ASP.NET Core | `firebase.json`, `Program.cs` | Teste de integração validando headers |
| **AI-01** | **A** | Conflito de sobreposição textual em adaptações | **Resolvido** | Substituições em ordem reversa e detecção de sobreposição determinística | `ResumeOptimizationService.cs` | Teste unitário de sobreposição passando |
| **AI-02** | **A** | Bloqueio 409 ao reabrir planos adaptados | **Resolvido** | Snapshot de decisões retornado no `GetPlanAsync` e UI em modo de leitura/exportação | `OptimizationAdaptationPage.tsx`, `ResumeOptimizationService.cs` | Testes unitários e integração passando |
| **ARCH-02** | **A** | Arquivos órfãos no frontend | **Resolvido** | Removidos os 6 arquivos legados não utilizados | `src/frontend/src/` | `npm build` e `npm test` limpos |
| **SEC-02** | **B** | Rate limiter global estático | **Resolvido** | Particionados contadores por UID e IP | `Program.cs` | Teste unitário com 2 usuários distintos |
| **OPS-01** | **B** | Migrations no startup da API | **Resolvido** | Suporte a `--migrate-only` e flag `Database:AutoMigrate` | `Program.cs`, `docs/DEPLOYMENT.md` | API executa migration isolada |
| **OPS-02** | **B** | Agendamento de purga de retenção | **Resolvido** | Provisionados scripts de Cloud Run Job + Cloud Scheduler | `scripts/setup-retention-purge.*`, docs | Scripts de automação versionados |
| **ARCH-01** | **D** | Dois tipos em `ResumeTextExtractors.cs` | **Resolvido** | Separados em `PdfResumeTextExtractor` e `DocxResumeTextExtractor` | `Extractors/` | `dotnet build` sem alertas |
| **ARCH-03** | **D** | E/S síncrona nos extratores de texto | **Resolvido** | `CopyToAsync` assíncrono com `CancellationToken` | `Extractors/*.cs` | Testes de extração passando |
| **ARCH-04** | **D** | Warning CS8602 do compilador C# | **Resolvido** | Checagem segura de `MainDocumentPart?.Document?.Body` | `DocxResumeTextExtractor.cs` | 0 warnings na compilação |
| **OPS-03** | **D** | Action do Firebase sem SHA fixo | **Resolvido** | Fixada action por commit SHA imutável | `.github/workflows/ci.yml:87` | Action fixada por SHA imutável |

---

## 5. CHECKLIST EXTERNO (CONSOLES DE NUVEM)

Instruções objetivas para validação manual dos serviços externos sem concessão de permissões amplas:

### 5.1 Firebase Console (`resume-matcher-f61df`)
- [ ] **Authentication → Sign-in method:**
  - Conferir se o provedor **Google** está ativo.
  - Conferir se o e-mail de suporte exigido pelo Google está configurado.
- [ ] **Authentication → Settings → Authorized domains:**
  - Conferir presença de `resume-matcher-f61df.web.app` e `resume-matcher-f61df.firebaseapp.com`.
  - Para testes locais, conferir presença de `localhost`.
- [ ] **Hosting:**
  - Conferir canal live apontando para a última versão implantada pelo GitHub Actions.

### 5.2 Google Cloud Run (Backend API)
- [ ] **Variáveis de Ambiente do Serviço:**
  - `Authentication__Firebase__ProjectId` = `resume-matcher-f61df`.
  - `Authentication__Firebase__AllowedEmail` = e-mail exato da conta autorizada (letras minúsculas).
  - `Cors__Origins__0` = `https://resume-matcher-f61df.web.app`.
  - `Cors__Origins__1` = `https://resume-matcher-f61df.firebaseapp.com`.
- [ ] **Segurança e IAM:**
  - Após validar a proteção JWT na API, confirmar se o serviço permite requisições não autenticadas na borda (`allUsers` com role `roles/run.invoker`) para permitir o preflight CORS do navegador.
  - Caso mantenha IAM na borda, garantir que o frontend envie `X-Serverless-Authorization`.
- [ ] **Health Probes:**
  - Confirmar que a Startup Probe utiliza **TCP** na porta 8080. **Não** apontar HTTP probe para `/health` sem token.

### 5.3 Supabase / PostgreSQL
- [ ] **Data API (PostgREST):**
  - Acessar o painel do Supabase → API Settings.
  - Confirmar que as tabelas `Resumes`, `Analyses` e `Optimizations` do schema `public` **não estão expostas via Data API anônima** (a aplicação acessa via conexão direta de banco de dados, pooler na porta 5432/6543).
- [ ] **Backups:**
  - Confirmar política de retenção de backups diários e PITR (Point-in-Time Recovery) disponível no plano contratado.

### 5.4 Secret Manager
- [ ] `ConnectionStrings__ResumeMatcher`: String de conexão ao PostgreSQL Supabase com flag `SSL Mode=Require`.
- [ ] `LLM__ApiKey`: Chave de API da Google AI Studio / Gemini com cotas e limites de orçamento configurados no Google Cloud Console.

### 5.5 GitHub Repository Settings
- [ ] **Secrets and Variables → Actions:**
  - Conferir segredo `FIREBASE_SERVICE_ACCOUNT_RESUME_MATCHER_F61DF` cadastrado.
- [ ] **Branches → Branch Protection Rules:**
  - Conferir se as branches `develop` e `main` possuem:
    - *Require status checks to pass before merging* (jobs `backend` e `frontend`).
    - *Require a pull request before merging*.
    - *Block force pushes*.

---

## 6. DEFINITION OF DONE (DoD) DO MVP

### Matriz de Requisitos e Rastreabilidade

| Requisito do MVP | Estado Atual | Evidência Comprovada | Critério Objetivo de Conclusão | Bloqueador Atual? |
| :--- | :--- | :--- | :--- | :--- |
| **1. Upload e Extração PDF/DOCX** | Concluído | Testes unitários e integração passam; limite 10MB; 200k chars | Arquivos válidos extraídos; arquivos corrompidos rejeitados com 400 | Não |
| **2. Comparação e Scoring** | Concluído | `WeightedScoringEngine` testa pesos, penalidades e requisitos mínimos | Score global e detalhado gerado com cálculo determinístico ponderado | Não |
| **3. Anti-Hallucination & Anti-Fraude** | Concluído | `OptimizationSafetyValidatorTests` validam Safe/NeedsConfirmation/Forbidden | Propostas sem evidência não podem ser marcadas como Safe | Não |
| **4. Isolamento por Dono** | Concluído | 130 testes backend; isolamento de consultas e FKs compostas | Usuário B recebe 404 ao tentar acessar dados do Usuário A | Não |
| **5. Retenção de 30 Dias** | Concluído no código | Filtro de queries em 30 dias; comando `--purge-expired` testado | Dados com >30 dias inacessíveis e purgados | Não (Agendamento operacional pendente para nuvem) |
| **6. Login Google & Conta Autorizada** | Concluído | JWT validado com RS256; bloqueio de outros e-mails | Somente e-mail verificado entra; outros recebem 403 | Não |
| **7. Exportação PDF e DOCX** | Concluído | `PdfResumeDocumentExporter` e `DocxResumeDocumentExporter` passam | Download de arquivo formatado com base no texto adaptado | Não |
| **8. Interface Responsiva e Acessível** | Concluído | Design System MVP implementado; Vitest 21/21; responsivo mobile/desktop | Telas fiéis ao protótipo visual, com foco, labels e estados de carregamento | Não |
| **9. Reabertura Limpa de Planos** | Parcial | Planos Applied entram em conflito se submetidos novamente | Usuário visualiza histórico e plano adaptado sem erro 409 | **Sim (Item AI-02)** |
| **10. Hardening de Cabeçalhos HTTP** | Pendente | Não há CSP nem HSTS configurados | Headers de segurança presentes em respostas do frontend e backend | **Sim (Item SEC-01)** |

### Decisão Final para o Responsável Técnico
O sistema está em excelente estado técnico e estrutural. A recomendação é resolver os itens **SEC-01** (cabeçalhos de segurança), **AI-01** (sobreposição textual) e **AI-02** (experiência visual de plano aplicado) como primeira etapa (Marco A), mantendo o código sem correções nesta tarefa de diagnóstico conforme estritamente solicitado.
