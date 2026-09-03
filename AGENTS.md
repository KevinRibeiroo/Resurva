# Contexto do projeto para agentes de IA

Leia este arquivo antes de modificar o repositório. Ele registra o contexto estável necessário para continuar o projeto sem depender do histórico de uma conversa.

## Objetivo

O ResumeMatcher compara currículos em PDF/DOCX com descrições de vagas. Ele apresenta scores e evidências para ajudar o candidato a entender aderência e lacunas. Não é um sistema de seleção de candidatos e não deve inventar informações para elevar o score.

## Estado real

- O MVP possui backend .NET 8 e frontend React.
- O upload, a extração, a comparação, a pontuação e a persistência local funcionam.
- O único provider suportado é `MockLLMProvider`.
- `Google.GenAI` está referenciado, mas ainda não existe um provider real implementado.
- SQLite usa `EnsureCreatedAsync`; ainda não há migrations.
- Não há autenticação, CI/CD nem implantação de produção.
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
- O banco SQLite local é `resumematcher.db` e está ignorado pelo Git.
- Pesos de scoring ficam em `appsettings.json` e devem somar `1`.

## Prioridades

1. Manter o pipeline atual estável e testado.
2. Concluir a organização de nomes e arquivos.
3. Adicionar testes de integração e CI.
4. Implementar provider real com saída estruturada e controles de privacidade.
5. Evoluir persistência, autenticação e retenção de dados.
6. Implementar otimização de currículo com confirmação explícita.

## Ao iniciar uma nova tarefa

- Leia `README.md`, este arquivo e os documentos relevantes em `docs/`.
- Verifique `git status` e preserve mudanças existentes do usuário.
- Confirme a implementação atual antes de confiar no roadmap.
- Faça a menor mudança coerente com a arquitetura.
- Execute validações proporcionais ao risco.
- Registre limitações ou próximos passos quando algo permanecer incompleto.
