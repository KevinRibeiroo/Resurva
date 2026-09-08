<#
.SYNOPSIS
    Provisiona o Cloud Run Job e o Cloud Scheduler para purga diária dos registros expirados (30 dias).
.DESCRIPTION
    Cria ou atualiza:
    1. Cloud Run Job executando a imagem da API com o argumento '--purge-expired'.
    2. Cloud Scheduler acionando o Job diariamente às 03:00 UTC.
#>
param (
    [string]$ProjectId = "resume-matcher-f61df",
    [string]$Region = "southamerica-east1",
    [string]$JobName = "resumematcher-purge-expired",
    [string]$SchedulerName = "resumematcher-purge-daily",
    [string]$ImageUri = "southamerica-east1-docker.pkg.dev/$ProjectId/resumematcher/api:latest",
    [string]$Schedule = "0 3 * * *",
    [string]$ServiceAccount = "resumematcher-scheduler@$ProjectId.iam.gserviceaccount.com"
)

$ErrorActionPreference = "Stop"

Write-Host "==> Configurando Cloud Run Job: $JobName na regiao $Region..." -ForegroundColor Cyan

# 1. Criar ou atualizar o Cloud Run Job
gcloud run jobs deploy $JobName `
    --project=$ProjectId `
    --region=$Region `
    --image=$ImageUri `
    --args="--purge-expired" `
    --set-secrets="ConnectionStrings__ResumeMatcher=ConnectionStrings__ResumeMatcher:latest" `
    --max-retries=1 `
    --task-timeout=10m

Write-Host "==> Cloud Run Job configurado com sucesso." -ForegroundColor Green

# 2. Criar ou atualizar o agendamento no Cloud Scheduler
Write-Host "==> Configurando Cloud Scheduler: $SchedulerName ($Schedule)..." -ForegroundColor Cyan

$jobUri = "https://$Region-run.googleapis.com/apis/run.googleapis.com/v1/namespaces/$ProjectId/jobs/$JobName:run"

$existing = gcloud scheduler jobs list --project=$ProjectId --location=$Region --filter="name:$SchedulerName" --format="value(name)"
if ($existing) {
    Write-Host "Atualizando agendador existente..."
    gcloud scheduler jobs update http $SchedulerName `
        --project=$ProjectId `
        --location=$Region `
        --schedule="$Schedule" `
        --uri="$jobUri" `
        --http-method=POST `
        --oauth-service-account-email="$ServiceAccount"
} else {
    Write-Host "Criando novo agendador..."
    gcloud scheduler jobs create http $SchedulerName `
        --project=$ProjectId `
        --location=$Region `
        --schedule="$Schedule" `
        --uri="$jobUri" `
        --http-method=POST `
        --oauth-service-account-email="$ServiceAccount"
}

Write-Host "==> Agendamento de purga de retencao configurado com sucesso!" -ForegroundColor Green
