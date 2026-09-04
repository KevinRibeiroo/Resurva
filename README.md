# ResumeMatcher

Aplicação web para comparar um currículo com uma descrição de vaga e apresentar uma análise estruturada de aderência. O objetivo é ajudar candidatos a identificar competências encontradas, lacunas e pontos de atenção sem inventar experiências ou qualificações.

> **Estado atual:** MVP funcional. A análise usa um provider local `Mock`, com vocabulário controlado e pontuação determinística. Nenhuma API externa é necessária para executar o projeto.

## Funcionalidades

- Upload de currículos em PDF e DOCX, com limite de 10 MB.
- Extração local do texto dos documentos.
- Comparação com a descrição de uma vaga.
- Pontuação geral e por dimensão: skills, experiência, senioridade, requisitos e formação.
- Evidências textuais para itens encontrados no currículo.
- Persistência local dos currículos e resultados em SQLite.
- Interface web para upload e visualização da análise.
- Validação de segurança para impedir sugestões que adicionem informações não confirmadas.

## Tecnologias

| Área | Tecnologias |
| --- | --- |
| Backend | C# 12, .NET 8, ASP.NET Core Web API |
| Persistência | Entity Framework Core 8 e SQLite |
| Extração de documentos | PdfPig e Open XML SDK |
| Frontend | React 19, TypeScript 5.9 e Vite 7 |
| Testes | xUnit, Microsoft.NET.Test.Sdk e coverlet |
| IA | Abstração `ILLMProvider`; implementação atual `MockLLMProvider` |

O pacote `Google.GenAI` está referenciado na infraestrutura, mas a integração real ainda não foi implementada. O projeto não envia currículos a serviços externos no estado atual.

## Arquitetura

O backend segue uma separação em camadas inspirada em Clean Architecture:

```text
Frontend React
      |
      v
ASP.NET Core API
      |
      v
Application  --->  Domain
      ^              ^
      |              |
Infrastructure -----+
```

- **Domain:** entidades e modelos centrais, sem dependências de infraestrutura.
- **Application:** contratos, serviços, regras de pontuação e casos de uso.
- **Infrastructure:** SQLite, repositórios, extração de PDF/DOCX e provider de comparação.
- **Api:** composição da aplicação, middleware HTTP e controllers.
- **Frontend:** experiência de upload, comparação e apresentação dos resultados.

Leia [Arquitetura](docs/ARCHITECTURE.md) para detalhes do fluxo, dependências e decisões técnicas.

## Estrutura do repositório

```text
ResumeProject/
|-- src/
|   |-- backend/
|   |   |-- ResumeMatcher.Api/
|   |   |-- ResumeMatcher.Application/
|   |   |-- ResumeMatcher.Domain/
|   |   `-- ResumeMatcher.Infrastructure/
|   `-- frontend/
|-- tests/
|   `-- ResumeMatcher.Tests/
|-- docs/
|-- AGENTS.md
|-- README.md
`-- ResumeMatcher.slnx
```

## Pré-requisitos

- [.NET SDK 8](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 20.19 ou superior](https://nodejs.org/)
- [pnpm](https://pnpm.io/installation)

## Executar localmente

### 1. Backend

Na raiz do repositório:

```powershell
dotnet restore ResumeMatcher.slnx
dotnet run --project src/backend/ResumeMatcher.Api
```

A API inicia em `http://localhost:5080` usando o perfil HTTP do projeto. O banco `resumematcher.db` é criado automaticamente e não deve ser versionado.

### 2. Frontend

Em outro terminal:

```powershell
cd src/frontend
pnpm install
pnpm dev
```

Abra `http://localhost:5173`. Durante o desenvolvimento, o Vite encaminha chamadas `/api` para o backend em `http://localhost:5080`.

## API

| Método | Endpoint | Descrição |
| --- | --- | --- |
| `POST` | `/api/resumes/upload` | Recebe `multipart/form-data` com o campo `file` em PDF ou DOCX. |
| `POST` | `/api/analysis/compare` | Compara o currículo enviado com uma descrição de vaga. |
| `GET` | `/api/analysis/{id}` | Recupera uma análise persistida pelo identificador. |

Exemplo do corpo para comparação:

```json
{
  "resumeId": "00000000-0000-0000-0000-000000000000",
  "jobDescription": "Descrição e requisitos da vaga"
}
```

Exemplos executáveis estão em [`Requests.http`](src/backend/ResumeMatcher.Api/Requests.http).

## Configuração

As configurações principais ficam em `src/backend/ResumeMatcher.Api/appsettings.json`:

- `ConnectionStrings:ResumeMatcher`: caminho do banco SQLite.
- `LLM:Provider`: somente `Mock` é suportado atualmente.
- `Scoring`: pesos das cinco dimensões; a soma deve ser igual a `1`.
- `Cors:Origins`: origens autorizadas a acessar a API.

Não versione chaves, tokens ou credenciais. Futuras integrações externas devem usar variáveis de ambiente ou o Secret Manager do .NET.

## Compilar e testar

```powershell
dotnet build ResumeMatcher.slnx
dotnet test tests/ResumeMatcher.Tests/ResumeMatcher.Tests.csproj

cd src/frontend
pnpm build
```

## Documentação

- [Arquitetura e fluxo técnico](docs/ARCHITECTURE.md)
- [Convenções e regras de desenvolvimento](docs/CONVENTIONS.md)
- [Estado atual e roadmap](docs/ROADMAP.md)
- [Contexto para manutenção e continuidade](AGENTS.md)

## Uso responsável

O ResumeMatcher deve destacar informações que já existem no currículo. Qualquer funcionalidade futura de otimização precisa distinguir sugestões de redação de alterações factuais e exigir confirmação explícita antes de incluir competências, experiências ou formações não comprovadas.

## Licença

O projeto ainda não possui uma licença definida. Antes de aceitar contribuições ou distribuir o software, adicione um arquivo `LICENSE` compatível com o objetivo do repositório.
