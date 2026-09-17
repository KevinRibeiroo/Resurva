#!/usr/bin/env bash
set -euo pipefail

# Provisiona o Cloud Run Job e Cloud Scheduler para purga diária dos registros expirados (30 dias).

PROJECT_ID="${PROJECT_ID:-resume-matcher-f61df}"
REGION="${REGION:-southamerica-east1}"
JOB_NAME="${JOB_NAME:-resumematcher-purge-expired}"
SCHEDULER_NAME="${SCHEDULER_NAME:-resumematcher-purge-daily}"
IMAGE_URI="${IMAGE_URI:-southamerica-east1-docker.pkg.dev/${PROJECT_ID}/resumematcher/api:latest}"
SCHEDULE="${SCHEDULE:-0 3 * * *}"
SERVICE_ACCOUNT="${SERVICE_ACCOUNT:-resumematcher-scheduler@${PROJECT_ID}.iam.gserviceaccount.com}"

echo "==> Configurando Cloud Run Job: ${JOB_NAME} na região ${REGION}..."

gcloud run jobs deploy "${JOB_NAME}" \
    --project="${PROJECT_ID}" \
    --region="${REGION}" \
    --image="${IMAGE_URI}" \
    --args="--purge-expired" \
    --set-secrets="ConnectionStrings__ResumeMatcher=ConnectionStrings__ResumeMatcher:latest" \
    --max-retries=1 \
    --task-timeout=10m

echo "==> Configurando Cloud Scheduler: ${SCHEDULER_NAME} (${SCHEDULE})..."

JOB_URI="https://${REGION}-run.googleapis.com/apis/run.googleapis.com/v1/namespaces/${PROJECT_ID}/jobs/${JOB_NAME}:run"

if gcloud scheduler jobs describe "${SCHEDULER_NAME}" --project="${PROJECT_ID}" --location="${REGION}" &>/dev/null; then
    echo "Atualizando agendador existente..."
    gcloud scheduler jobs update http "${SCHEDULER_NAME}" \
        --project="${PROJECT_ID}" \
        --location="${REGION}" \
        --schedule="${SCHEDULE}" \
        --uri="${JOB_URI}" \
        --http-method=POST \
        --oauth-service-account-email="${SERVICE_ACCOUNT}"
else
    echo "Criando novo agendador..."
    gcloud scheduler jobs create http "${SCHEDULER_NAME}" \
        --project="${PROJECT_ID}" \
        --location="${REGION}" \
        --schedule="${SCHEDULE}" \
        --uri="${JOB_URI}" \
        --http-method=POST \
        --oauth-service-account-email="${SERVICE_ACCOUNT}"
fi

echo "==> Agendamento de purga de retenção configurado com sucesso!"
