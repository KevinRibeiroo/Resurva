FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY src/backend/ResumeMatcher.Domain/ResumeMatcher.Domain.csproj src/backend/ResumeMatcher.Domain/
COPY src/backend/ResumeMatcher.DocumentLayout/ResumeMatcher.DocumentLayout.csproj src/backend/ResumeMatcher.DocumentLayout/
COPY src/backend/ResumeMatcher.Application/ResumeMatcher.Application.csproj src/backend/ResumeMatcher.Application/
COPY src/backend/ResumeMatcher.Infrastructure/ResumeMatcher.Infrastructure.csproj src/backend/ResumeMatcher.Infrastructure/
COPY src/backend/ResumeMatcher.Api/ResumeMatcher.Api.csproj src/backend/ResumeMatcher.Api/
RUN dotnet restore src/backend/ResumeMatcher.Api/ResumeMatcher.Api.csproj

COPY src/backend/ src/backend/
RUN dotnet publish src/backend/ResumeMatcher.Api/ResumeMatcher.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "ResumeMatcher.Api.dll"]
