# help-nfse — imagem única: API ASP.NET Core (servindo o frontend Angular) + worker .NET.
# Construída somente no CI (ghcr.io/brskt-dev/help-nfse:sha-<commit>); nunca na VPS.
#   API (padrão):  dotnet /app/api/HelpNfse.Api.dll
#   Migrações:     dotnet /app/api/HelpNfse.Api.dll --migrate
#   Worker:        dotnet /app/worker/HelpNfse.Worker.dll

ARG NODE_IMAGE=node:22-alpine

# ---------- Frontend ----------
FROM ${NODE_IMAGE} AS web
WORKDIR /web
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY frontend/ ./
RUN npx ng build --configuration production

# ---------- Backend ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY backend/Directory.Build.props backend/Directory.Packages.props ./
COPY backend/src/HelpNfse.Core/HelpNfse.Core.csproj src/HelpNfse.Core/
COPY backend/src/HelpNfse.Api/HelpNfse.Api.csproj src/HelpNfse.Api/
COPY backend/src/HelpNfse.Worker/HelpNfse.Worker.csproj src/HelpNfse.Worker/
RUN dotnet restore src/HelpNfse.Api/HelpNfse.Api.csproj \
 && dotnet restore src/HelpNfse.Worker/HelpNfse.Worker.csproj
COPY backend/src/ src/
# appsettings.Development.json e a página provisória de wwwroot não são publicados (ver .csproj).
RUN dotnet publish src/HelpNfse.Api/HelpNfse.Api.csproj -c Release -o /out/api --no-restore \
 && dotnet publish src/HelpNfse.Worker/HelpNfse.Worker.csproj -c Release -o /out/worker --no-restore
COPY --from=web /web/dist/frontend/browser/ /out/api/wwwroot/

# ---------- Runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0
# curl só para o healthcheck do container.
RUN apt-get update && apt-get install -y --no-install-recommends curl && apt-get clean && find /var/lib/apt/lists -type f -delete
COPY --from=build /out/ /app/
WORKDIR /app/api
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_gcServer=0 \
    DOTNET_NOLOGO=1
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "/app/api/HelpNfse.Api.dll"]
