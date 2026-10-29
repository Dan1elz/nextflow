# Setup local — API

> Só este repositório. Para subir o produto inteiro (API + front), veja `../nextflow-docs/docs/process/local-setup.md`.
> Detalhes de homologação e variáveis: `docs/DOCKER.MD`.

## Pré-requisitos
Docker Desktop; .NET 10 SDK (para rodar fora do Docker e para os testes, que sobem um Postgres pelo Testcontainers).

## Subir
O compose de dev da API nova (`nextflow-v2-development`) tem projeto, portas e volumes próprios: roda ao lado do dev
antigo (`nextflow-development`, Postgres em 5432) sem tocar no banco dele. O `.env` é opcional; sem ele valem os padrões.
```
docker compose -f compose.development.yaml up -d --build
```
API em `http://localhost:8081` (health `/health`, Swagger em `/swagger`), PostgreSQL em `localhost:5435`
(`nextflow_db`, `nextflow_user`, `nextflow_password`), storage S3 em `http://localhost:9010` (painel `:9011`).
O `storage-initial-setup` termina com `Exited (0)` e "Storage pronto.": é o normal. Migrations e seeders rodam no startup.

Fora do Docker (só o banco do compose no ar):
```
docker compose -f compose.development.yaml up -d postgres
dotnet run --project nextflow/Nextflow.csproj      # http://localhost:5145
```

Recriar o banco de dev do zero: `docker compose -f compose.development.yaml down -v` e subir de novo.

## Comandos úteis
```
docker compose -f compose.development.yaml logs -f
docker compose -f compose.development.yaml exec <serviço> sh
dotnet build nextflow-erp.sln
dotnet test nextflow-erp.sln
dotnet ef migrations add <Nome> --project nextflow.Infrastructure --startup-project nextflow
```

## IA
`AI_API_KEY` vazio = previsão só com o cálculo (status "Concluída sem IA"). A chave nunca vai para o repositório.
