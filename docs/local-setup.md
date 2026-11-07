# Setup local — API

> Só este repositório. Para subir o produto inteiro (API + front), veja `../nextflow-docs/docs/process/local-setup.md`.
> Detalhes de homologação e variáveis: `docs/DOCKER.MD`.

## Pré-requisitos
Docker Desktop; .NET 8 SDK (para rodar fora do Docker). Rede compartilhada, uma vez:
`docker network inspect nextflow-development-network >/dev/null 2>&1 || docker network create nextflow-development-network`

## Subir
```
cp .env.example .env
docker compose -f compose.development.yaml up -d --build
```
API em `http://localhost:8080` (health `/health`, Swagger em `/swagger`), PostgreSQL em `localhost:5432`,
storage S3 em `http://localhost:9000` (painel `:9001`). O `storage-initial-setup` termina com `Exited (0)` e
"Storage pronto.": é o normal. Migrations e seeders rodam no startup; o seed demo (`Seed:Demo:Enabled`) leva ~30 s.

Fora do Docker (com banco e storage do compose no ar): `dotnet run --project nextflow/Nextflow.csproj`.

## Comandos úteis
```
docker compose -f compose.development.yaml logs -f
docker compose -f compose.development.yaml exec <serviço> sh
docker compose -f compose.development.yaml down
dotnet build nextflow-erp.sln
```

## IA
`AI_API_KEY` vazio = previsão só com o cálculo (status "Concluída sem IA"). A chave nunca vai para o repositório.
