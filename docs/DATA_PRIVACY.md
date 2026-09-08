# Isolamento, retenção e exclusão

## Decisões aprovadas

- Currículos e análises expiram após **30 dias desde a última atualização**, em UTC.
- Nova análise gravada atualiza o currículo utilizado e a própria análise, sem renovar análises anteriores.
- Consultas, login e cache hit não renovam o prazo.
- Excluir uma conta deve apagar todos os seus currículos e análises.
- Backups: **máximo de 30 dias após a exclusão**; configuração/verificação no provedor pendentes.
- Visitantes: armazenamento temporário, com duração ainda a definir. O modo visitante não existe nesta etapa.

## Implementado

`ICurrentUser` obtém o `sub` do token Firebase validado. Identidade ausente, não autenticada ou UID vazio são recusados. O dono não vem do formulário/JSON do frontend. A autorização privada por um único e-mail permanece; esta mudança não libera cadastro multiusuário.

Currículos e análises têm `OwnerUserId`. Consultas e exclusões exigem o dono atual. Comparar currículo alheio, consultar análise alheia e excluir currículo alheio retornam `404`. A FK composta `(OwnerUserId, ResumeId)` aponta para `(OwnerUserId, Id)` do currículo e impede vínculos entre donos diferentes no PostgreSQL.

Cache: índice único `(OwnerUserId, AnalysisInputHash)`, consultas e tratamento de conflito separados por dono. A chave de compartilhamento de análises em andamento também inclui o UID. Não é uma política RLS do Supabase: acesso direto ao banco é privilegiado e deve permanecer restrito à API/manutenção autorizada.

`UpdatedAt` é definido no servidor. Registros com `UpdatedAt <= agora - 30 dias` ficam inacessíveis para consulta, comparação e cache mesmo antes da limpeza física. Exclusão manual do próprio currículo continua permitida inclusive se expirado, com análises em cascata.

`RetentionCleanupService` remove registros expirados; no PostgreSQL, usa transação e exclusões SQL. É uma operação administrativa sem endpoint HTTP. O comando encerra após a execução, sem iniciar o servidor HTTP:

```powershell
dotnet run --project src/backend/ResumeMatcher.Api -- --purge-expired
```

Na imagem já construída, passe `--purge-expired` ao entrypoint existente. Usa a connection string do ambiente e **apaga dados de verdade**. Revise alvo, migration, dados legados e backups antes de executar. Não há timer dependente de uma instância HTTP que pode escalar a zero.

**Agendamento operacional:** scripts versionados de automação estão disponíveis em `scripts/setup-retention-purge.ps1` e `scripts/setup-retention-purge.sh`. Eles criam o Cloud Run Job com `--purge-expired` e o Cloud Scheduler com periodicidade diária (03:00 UTC). A expiração de acesso está garantida no código pelas queries de leitura (`UpdatedAt > agora - 30 dias`), e a limpeza física diária é garantida por esse agendador.

`IAccountDataService.DeleteAllAsync` prepara remoção transacional e idempotente dos registros do usuário atual, incluindo expirados, sem afetar outras contas. **Não é um endpoint de encerramento de conta e não apaga a identidade Firebase.** Não apresentar essa operação como exclusão completa: sessões válidas ainda poderiam criar novos dados.

## Exclusão completa de conta: pendente

Antes de oferecer a ação na interface, implementar confirmação/reautenticação, bloqueio de sessões e novas gravações, exclusão dos dados, remoção da identidade Firebase e recuperação de falhas entre sistemas. Apagar no Firebase não dispara limpeza neste backend; apagar só no banco não encerra o acesso. A API ainda não consulta revogação Firebase em cada requisição.

## Migration e dados existentes

`AddDataOwnershipAndRetention` adiciona colunas e mantém `OwnerUserId` vazio nos registros antigos. `UpdatedAt` da análise recebe seu `CreatedAt`; no currículo recebe a data mais recente entre sua criação e a criação de suas análises. Assim preserva a última gravação conhecida, sem renovar tudo para a data do deploy. Nenhum login reivindica registros legados. Eles continuam no banco, invisíveis até atribuição explícita, e podem estar expirados conforme sua idade real.

**A API aplica migrations ao iniciar: o primeiro deploy desta branch mudará o esquema.** Antes do merge que aciona deploy, revisar os registros e confirmar o UID do proprietário no projeto Firebase correto. Procedimento de transferência:

1. Suspender gravações e manter o ambiente privado. Não executar a limpeza ainda.
2. Aplicar a migration em janela controlada.
3. Confirmar explicitamente o UID e os IDs dos currículos a transferir; não presumir que todos têm o mesmo dono.
4. Em transação administrativa, executar `SET CONSTRAINTS "FK_Analyses_Resumes_OwnerUserId_ResumeId" DEFERRED`; atualizar `OwnerUserId` dos currículos selecionados e das análises correspondentes, apenas quando o dono antigo estiver vazio. Conferir quantidades antes do commit. Não alterar `UpdatedAt` apenas para renovar retenção.
5. Verificar os dados pelo backend antes de liberar gravações/agendamento.

O rollback recusa remover ownership quando há dados com dono atribuído. Não publicar API antiga com consultas globais nesse banco. Recuperação exige plano revisado.

## Backups, logs e serviços externos

Até 30 dias após exclusão é o limite aprovado, não uma garantia de que o plano atual oferece exatamente 30 dias de recuperação. Inventariar backups gerenciados, PITR, dumps e exportações. Após restore, reaplicar exclusões e expirações antes de servir tráfego; o mecanismo de reconciliação das exclusões ainda precisa ser implementado.

Conferir a configuração real e a [documentação de backups do Supabase](https://supabase.com/docs/guides/platform/backups): disponibilidade e retenção dependem da modalidade/plano. O código não altera o ciclo de vida dessas cópias.

Logs explícitos de autenticação/upload deixaram de registrar e-mail, UID, nome de arquivo e token. Não habilitar sensitive-data logging do EF. A revisão integral dos logs do provider, os logs já existentes e a retenção no serviço Gemini continuam como tarefas separadas; apagar no PostgreSQL não equivale a apagar no fornecedor externo.

## Testes

Testes HTTP usam JWTs sintéticos com UIDs diferentes (mesmo e-mail autorizado para exercitar ownership sem enfraquecer a política de produção). Cobrem dono falsificado no upload, acesso/exclusão cruzados, cache privado, dados legados, fronteira de 30 dias, leitura sem renovação e atualização por nova análise.

O teste PostgreSQL cobre migration incremental com dados antigos, snapshot/modelo, FK composta, índice de cache, cascata, limpeza física e operação interna de apagar os dados de uma conta. Nenhum teste utiliza currículos reais ou o Supabase.
