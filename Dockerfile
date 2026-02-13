# 1. Estágio de Build
ARG DOTNET_VERSION=10.0
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION}-alpine AS build
WORKDIR /app

# Instala dependências
RUN apk add --no-cache icu-libs

# 2. Copia a Solution e os Projetos (Restore Otimizado)
COPY *.slnx . 
COPY src/AgrosolutionsWorkerSensors.Domain/*.csproj ./src/AgrosolutionsWorkerSensors.Domain/
COPY src/AgrosolutionsWorkerSensors.Application/*.csproj ./src/AgrosolutionsWorkerSensors.Application/
COPY src/AgrosolutionsWorkerSensors.Infrastructure/*.csproj ./src/AgrosolutionsWorkerSensors.Infrastructure/
COPY src/AgrosolutionsWorkerSensors.Host/*.csproj ./src/AgrosolutionsWorkerSensors.Host/
COPY src/AgrosolutionsWorkerSensors.Generator/*.csproj ./src/AgrosolutionsWorkerSensors.Generator/

RUN dotnet restore

# 3. Copia o restante do código fonte
COPY src/ ./src/

# 4. Publica o HOST (Consumidor)
RUN dotnet publish src/AgrosolutionsWorkerSensors.Host/AgrosolutionsWorkerSensors.Registration.csproj -c Release -o /app/publish/host

# 5. Publica o GENERATOR (Produtor)
RUN dotnet publish src/AgrosolutionsWorkerSensors.Generator/AgrosolutionsWorkerSensors.Generator.csproj -c Release -o /app/publish/generator

# --- Estágio Final (Runtime) ---
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-alpine AS final
WORKDIR /app

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
RUN apk add --no-cache icu-libs

# Copia os artefatos compilados
COPY --from=build /app/publish/host ./host
COPY --from=build /app/publish/generator ./generator

RUN chown -R 0:0 /app && chmod -R g+w /app

# Entrypoint padrão
ENTRYPOINT ["dotnet", "host/AgrosolutionsWorkerSensors.Registration.dll"]