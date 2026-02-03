ARG DOTNET_VERSION=10.0
# ARG NODE_VERSION=22

# FROM node:${NODE_VERSION}-bookworm-slim AS ui-build
# WORKDIR /src/pricer-ui
# COPY pricer-ui/package*.json ./
# RUN npm ci
# COPY pricer-ui/ ./
# RUN npm run build

FROM mcr.microsoft.com/playwright/dotnet:v1.39.0-jammy AS playwright-browsers

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src
COPY Pricer.Api/Pricer.Api.csproj Pricer.Api/
COPY Pricer.Application/Pricer.Application.csproj Pricer.Application/
COPY Pricer.Infrastructure/Pricer.Infrastructure.csproj Pricer.Infrastructure/
COPY Pricer.Domain/Pricer.Domain.csproj Pricer.Domain/
RUN dotnet restore Pricer.Api/Pricer.Api.csproj
COPY . .
RUN dotnet build -c Debug Pricer.Api/Pricer.Api.csproj /p:UseAppHost=false
# RUN dotnet publish Pricer.Api/Pricer.Api.csproj -c Release -o /app/publish /p:UseAppHost=false
# COPY --from=ui-build /src/pricer-ui/dist/ /app/publish/wwwroot/

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Development
ENV ASPNETCORE_URLS=http://+:8080
ENV PLAYWRIGHT_BROWSERS_PATH=/ms-playwright
ENV PLAYWRIGHT_SKIP_BROWSER_DOWNLOAD=1
EXPOSE 8080

COPY --from=playwright-browsers /ms-playwright /ms-playwright
COPY --from=build /src/Pricer.Api/bin/Debug/net10.0/ .
ENTRYPOINT ["dotnet", "Pricer.Api.dll"]
