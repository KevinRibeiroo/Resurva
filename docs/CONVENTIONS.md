# Convenções de desenvolvimento

Estas regras valem para código novo e para arquivos alterados durante refatorações.

## Idioma

- Identificadores de código, nomes de arquivos, commits técnicos e APIs usam inglês.
- Textos apresentados ao usuário e a documentação principal usam português.
- Termos consolidados como `Resume`, `Analysis`, `Repository` e `Provider` devem permanecer consistentes entre as camadas.

## Arquivos e tipos C#

- Use um tipo público principal por arquivo.
- O arquivo deve ter exatamente o mesmo nome do tipo público.
- Entidades persistidas terminam em `Entity`: `ResumeEntity.cs`, `AnalysisEntity.cs`.
- Modelos e DTOs terminam em `Model`: `AnalysisResultModel.cs`, `CompareRequestModel.cs`.
- Interfaces começam com `I`: `IResumeRepository.cs`, `ILLMProvider.cs`.
- Implementações de repositório terminam em `Repository`.
- Serviços de aplicação terminam em `Service`.
- Controllers terminam em `Controller`.
- Exceções terminam em `Exception`.
- Opções de configuração terminam em `Options`.
- Comandos terminam em `Command`.

Não use nomes genéricos de arquivos como `Models.cs`, `Contracts.cs`, `Services.cs` ou `Persistence.cs` para agrupar vários tipos públicos.

## Organização por camada

- `Domain/Entities`: entidades persistidas.
- `Domain/Models`: modelos centrais do domínio.
- `Application/Interfaces`: contratos da aplicação.
- `Application/Commands`: entradas de casos de uso.
- `Application/Models`: DTOs e resultados internos da aplicação.
- `Application/Services`: implementação dos casos de uso.
- `Application/Options`: classes ligadas à configuração.
- `Application/Exceptions`: erros esperados da aplicação.
- `Infrastructure/Persistence`: DbContext e repositórios concretos.
- `Infrastructure/Providers`: integrações e providers concretos.
- `Infrastructure/Extractors`: extratores de documentos.
- `Api/Controllers`: endpoints HTTP.
- `Api/Models`: requests e responses específicos da API.

Pastas não devem forçar namespaces acoplados à estrutura física. Preserve os namespaces de cada projeto enquanto isso mantiver o uso simples e coerente.

## Dependências

- Domain não conhece Application, Infrastructure ou Api.
- Application não conhece implementações concretas.
- Infrastructure implementa interfaces definidas pela Application.
- Controllers apenas validam a entrada HTTP, chamam serviços e transformam o resultado em resposta.
- Regras de negócio não devem ficar em controllers, DbContext ou componentes React.

## Código

- Nullable reference types permanecem habilitados.
- Prefira APIs assíncronas para I/O e propague `CancellationToken`.
- Não capture exceções apenas para ignorá-las.
- Evite comentários que repetem o código; documente decisões e restrições.
- Preserve compatibilidade do contrato JSON ao renomear tipos C#.
- Não inclua segredos ou dados reais de currículos em código, testes, logs ou commits.

## Segurança funcional

- Nunca transforme ausência de evidência em experiência confirmada.
- Sugestões de redação podem reorganizar fatos existentes, mas não criar fatos.
- Alterações factuais exigem confirmação explícita do usuário.
- Casos proibidos devem continuar bloqueados mesmo quando solicitados por automação.

## Testes e validação

Antes de concluir uma mudança no backend:

```powershell
dotnet build ResumeMatcher.slnx
dotnet test tests/ResumeMatcher.Tests/ResumeMatcher.Tests.csproj
```

Antes de concluir uma mudança no frontend:

```powershell
cd src/frontend
pnpm build
```

Adicione ou atualize testes quando houver mudança em regras, parsing, pontuação, persistência ou contrato de API.

## Git

- Não versione `bin`, `obj`, `node_modules`, `dist`, `.vs` ou bancos SQLite locais.
- Faça commits pequenos, com uma responsabilidade clara.
- Não reescreva alterações locais de outra pessoa.
- Mudanças de arquitetura ou de contrato público devem atualizar a documentação no mesmo pull request.
