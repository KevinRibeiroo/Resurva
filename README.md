# ResumeMatcher

MVP para comparar um currículo em PDF/DOCX com uma descrição de vaga. O backend usa um provider Mock e calcula o score de forma determinística; nenhuma API externa é necessária.

## Executar

```powershell
dotnet run --project src/backend/ResumeMatcher.Api --urls http://localhost:5080
```

Em outro terminal (Node 20.19+):

```powershell
cd src/frontend
pnpm install
pnpm dev
```

## API

- `POST /api/resumes/upload` — multipart com campo `file` (PDF ou DOCX, até 10 MB)
- `POST /api/analysis/compare` — `{ "resumeId": "...", "jobDescription": "..." }`
- `GET /api/analysis/{id}`

O Mock identifica um vocabulário controlado e existe apenas para validar o pipeline. Um provider real substituirá `ILLMProvider` em uma etapa posterior.
