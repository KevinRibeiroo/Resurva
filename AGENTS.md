# Contexto do projeto para agentes de IA

Leia este arquivo antes de modificar o repositório. Ele registra o contexto estável necessário para continuar o projeto sem depender do histórico de uma conversa.

## Objetivo

O ResumeMatcher compara currículos em PDF/DOCX com descrições de vagas. Ele apresenta scores e evidências para ajudar o candidato a entender aderência e lacunas. Não é um sistema de seleção de candidatos e não deve inventar informações para elevar o score.

## Estado real

- O MVP possui backend .NET 10 (C# 14) e frontend React 19.2 (Vite 8).
- O upload, a extração, a comparação, a pontuação e a persistência local funcionam.
- `MockLLMProvider` é o padrão; `GeminiLLMProvider` pode ser habilitado por configuração.
- A persistência usa PostgreSQL/Npgsql e migrations do Entity Framework Core.
- Análises idênticas são reutilizadas pelo `AnalysisInputHash`; modelo, versão do prompt e versão das regras fazem parte da chave.
- O fingerprint também inclui as configurações de scoring e a configuração relevante do provider para invalidar resultados quando o comportamento mudar.
- A API possui `/health`, rate limiting, limites de entrada e exclusão em cascata de currículo/análises.
- O Gemini possui timeout e uma única nova tentativa por padrão para falhas temporárias.
- O código possui login Google via Firebase Authentication, validação JWT na API e autorização para um único e-mail verificado configurado no backend. A ativação no Firebase e a transição do IAM do Cloud Run exigem o procedimento de `docs/AUTHENTICATION.md`.
- Currículos, análises e cache são isolados por OwnerUserId obtido do token. O acesso privado de uma conta permanece; não existe modo visitante.
- Retenção: 30 dias desde a última atualização; leitura/cache hit não renovam. Comando de limpeza implementado, agendamento externo pendente. Backups: máximo 30 dias após exclusão, a verificar no provedor.
- A migration de ownership preserva dados antigos sem dono e usa a última gravação conhecida (criação do currículo/análises) como UpdatedAt. Atribuição ao UID exige revisão antes do rollout/limpeza, conforme docs/DATA_PRIVACY.md.
- Workflow CI versionado para PRs/push em develop/main; ativação e checks obrigatórios no GitHub dependem de configuração externa. O gatilho de deploy não espera o CI pós-push.
- Exclusão completa de conta Firebase pendente. Existe operação interna de apagar dados do usuário, sem endpoint de encerramento de conta.
- Consulte `docs/ROADMAP.md` antes de afirmar que algo futuro já está disponível.

## Arquitetura

- `ResumeMatcher.Domain`: entidades e modelos centrais; nenhuma dependência externa.
- `ResumeMatcher.Application`: contratos, casos de uso, scoring e segurança.
- `ResumeMatcher.Infrastructure`: banco, repositórios, extratores e providers.
- `ResumeMatcher.Api`: controllers, middleware, DI e configuração HTTP.
- `src/frontend`: cliente React/TypeScript.
- `tests/ResumeMatcher.Tests`: testes automatizados do backend.

Os detalhes e diagramas estão em `docs/ARCHITECTURE.md`.

## Regras obrigatórias

1. Preserve a direção das dependências entre camadas.
2. Um tipo público principal por arquivo; o nome do arquivo deve corresponder ao tipo.
3. Entidades terminam em `Entity`.
4. Modelos e DTOs terminam em `Model`.
5. Interfaces começam com `I` e ficam em arquivo próprio, como `IResumeRepository.cs`.
6. Não crie arquivos agregadores como `Models.cs`, `Contracts.cs`, `Services.cs` ou `Persistence.cs`.
7. Não altere o contrato JSON acidentalmente ao renomear tipos internos.
8. Propague `CancellationToken` em operações assíncronas.
9. Nunca registre, versione ou use em testes dados pessoais reais de currículos.
10. Nunca adicione ao currículo fatos sem evidência e confirmação do usuário.
11. Não inclua segredos no repositório.
12. Atualize a documentação ao mudar arquitetura, setup, API ou comportamento público.

Consulte também `docs/CONVENTIONS.md`.

## Comandos de validação

Backend:

```powershell
dotnet build ResumeMatcher.slnx
dotnet test tests/ResumeMatcher.Tests/ResumeMatcher.Tests.csproj
```

Frontend:

```powershell
cd src/frontend
pnpm install
pnpm build
```

Não conclua mudanças com erros de build ou testes conhecidos sem descrevê-los claramente.

## Configuração local

- Backend HTTP: `http://localhost:5080`.
- Frontend Vite: `http://localhost:5173`.
- O proxy `/api` deve apontar para a porta `5080`.
- A conexão PostgreSQL vem de `ConnectionStrings:ResumeMatcher`; credenciais locais devem ficar em variável de ambiente ou Secret Manager.
- Configure `Authentication:Firebase:ProjectId` e `Authentication:Firebase:AllowedEmail`; a API recusa iniciar sem a conta autorizada. Não crie bypass de autenticação no ambiente de desenvolvimento.
- Todos os endpoints, inclusive `/health`, exigem o token Firebase da conta autorizada. O preflight CORS das origens permitidas é atendido antes da autenticação.
- A API aplica migrations automaticamente ao iniciar.
- `DELETE /api/resumes/{id}` remove o currículo e suas análises relacionadas.
- O Dockerfile do backend usa a porta `8080`; os dados da API devem permanecer restritos à conta autorizada. Só retire a barreira IAM após publicar e validar a proteção JWT na API, conforme `docs/AUTHENTICATION.md`.
- Pesos de scoring ficam em `appsettings.json` e devem somar `1`.

## Prioridades

1. Manter o pipeline atual estável e testado.
2. Concluir a organização de nomes e arquivos.
3. Adicionar testes de integração e CI.
4. Reforçar os controles de privacidade e resiliência do provider real.
5. Evoluir autenticação, retenção de dados e estratégia de backup.
6. Implementar otimização de currículo com confirmação explícita.

## Ao iniciar uma nova tarefa

- Leia `README.md`, este arquivo e os documentos relevantes em `docs/`.
- Verifique `git status` e preserve mudanças existentes do usuário.
- Confirme a implementação atual antes de confiar no roadmap.
- Faça a menor mudança coerente com a arquitetura.
- Execute validações proporcionais ao risco.
- Registre limitações ou próximos passos quando algo permanecer incompleto.
