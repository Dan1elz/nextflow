# ============================
# Estágio de Build (SDK)
# ============================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar solução e projetos para restore otimizado (cache layer)
COPY *.sln ./
COPY nextflow/*.csproj ./nextflow/
COPY nextflow.Application/*.csproj ./nextflow.Application/
COPY nextflow.Domain/*.csproj ./nextflow.Domain/
COPY nextflow.Infrastructure/*.csproj ./nextflow.Infrastructure/

# Configurar diretório de pacotes para o restore
ENV NUGET_PACKAGES=/root/.nuget/packages

# Restore das dependências
RUN dotnet restore

# Copiar todo o código fonte
COPY . .

# Build e Publish da aplicação (Release)
# Ajuste o caminho do projeto principal se necessário
RUN dotnet publish nextflow/Nextflow.csproj -c Release -o /app/publish /p:UseAppHost=false

# ============================
# Estágio de Runtime (Imagem final)
# ============================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Instalar curl para o healthcheck e criar usuário sem privilégios (segurança)
RUN apt-get update && apt-get install -y --no-install-recommends curl \
  && rm -rf /var/lib/apt/lists/* \
  && adduser --disabled-password --gecos '' appuser \
  && chown -R appuser /app

USER appuser

# Configurações padrão de container
ENV ASPNETCORE_URLS=http://+:8080 \
  ASPNETCORE_ENVIRONMENT=Production \
  DOTNET_RUNNING_IN_CONTAINER=true

# Copiar os binários gerados no estágio de build
COPY --from=build --chown=appuser:appuser /app/publish .

EXPOSE 8080

# Healthcheck
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
  CMD curl --fail --silent --max-time 2 http://localhost:8080/health || exit 1

CMD ["dotnet", "nextflow.dll"]