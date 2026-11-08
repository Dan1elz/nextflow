# Setup local — API

> Só este repositório. Para subir o produto inteiro (API + front) numa máquina nova, veja
> `../nextflow-docs/docs/process/local-setup.md` e `../nextflow-docs/docs/HANDOFF.md`.
> Detalhes de homologação e variáveis: `docs/DOCKER.MD`. Conferido contra `compose.development.yaml`, `.env.example`,
> `appsettings.Development.json` e `UsersSeeder` em 07/11/2026.

## Pré-requisitos
- WSL 2 (Ubuntu) com o repo clonado **dentro do Linux** (ex.: `~/Projetos/nextflow/nextflow`), não em `/mnt/c`: o
  `dotnet watch` e o bind mount ficam lentos e o build pode quebrar com caminhos do Windows.
- Docker Desktop com a integração WSL ligada para a distro.
- .NET 10 SDK (só para rodar fora do Docker, gerar migration e rodar os testes, que sobem um Postgres pelo
  Testcontainers).
- Repo do front irmão em `../nextflow-front` (só se for usar o `--profile front`).

## `.env`
Opcional: **sem `.env` valem os padrões do compose**, e é assim que o dev roda hoje. Se criar um a partir do
`.env.example`, atenção:
- `STORAGE_PORT=9000`, `STORAGE_CONSOLE_PORT=9001` e `STORAGE_PUBLIC_URL=http://localhost:9000` do exemplo trocam as
  portas padrão do storage (9010/9011). Apague essas três linhas para manter os padrões.
- `SEED_ADMIN_PASSWORD` **não tem efeito no dev**: o compose de dev não a repassa como `Seed__Admin__Password` (só o
  `compose.staging.yaml` e o `compose.homolog.yaml` fazem isso). No dev vale a senha de desenvolvimento do `UsersSeeder`.
- Qualquer chave no formato .NET (ex.: `Seed__Demo__HistoryMonths=6`) no `.env` chega à API pelo `env_file`.
- `.env` antigos do v1 (outra senha de Postgres e outras portas) não servem para este compose.

## Subir
O compose de dev da API nova (`nextflow-v2-development`) tem projeto, portas e volumes próprios: roda ao lado do dev
antigo (`nextflow-development`, Postgres em 5432) sem tocar no banco dele.
```
docker compose -f compose.development.yaml up -d --build
```
- API em `http://localhost:8081` (health `/health`, Swagger em `/swagger`), em `dotnet watch` (hot reload).
- PostgreSQL em `localhost:5435` (`nextflow_db`, `nextflow_user`, `nextflow_password`).
- Storage S3 (Silo) em `http://localhost:9010`, painel em `:9011`. O `storage-initial-setup` termina com `Exited (0)` e
  "Storage pronto.": é o normal.
- Front buildado no nginx (`localhost:8082`) só com `--profile front`
  (`docker compose -f compose.development.yaml --profile front up -d --build`). No dia a dia use o compose de dev do
  próprio front (hot reload em `localhost:5174`). Ele espera o repo do front em `../nextflow-front` (outro caminho:
  `FRONT_CONTEXT`).

## Migrations e seed
- Migrations EF e seeders rodam **no startup** da API (`Database.MigrateAsync()` no `Program.cs`): não há passo manual.
- Seed demo (loja de doces e presentes): ligado por `Seed:Demo:Enabled` (no compose: `SEED_DEMO_ENABLED`, padrão
  `true`). O histórico vem de `Seed:Demo:HistoryMonths`, que no dev é **36** pelo `appsettings.Development.json` (o
  compose de dev não define essa variável; para mudar, `Seed__Demo__HistoryMonths` no `.env`). `SEED_DEMO_ANCHOR_DATE`
  fixa o "hoje" da simulação (vazio = hoje).
- Com 36 meses, o primeiro startup em banco vazio leva de 3 a 5 minutos: acompanhe em
  `docker compose -f compose.development.yaml logs -f backend`.
- Login inicial: `admin@nextflow.com`, com a senha de desenvolvimento definida em `UsersSeeder.DevelopmentPassword`
  (`nextflow.Infrastructure/Seeders/UsersSeeder.cs`). Só é criado com o banco vazio.
- Migration nova ou seed alterado (ex.: lotes da F1.7, cidades de 07/11) pedem banco limpo para ver os dados demo:
  `docker compose -f compose.development.yaml down -v` e subir de novo.

Fora do Docker (só o banco e o storage do compose no ar):
```
docker compose -f compose.development.yaml up -d postgres storage storage-initial-setup
dotnet run --project nextflow/Nextflow.csproj      # http://localhost:5145
```

## Comandos úteis
```
docker compose -f compose.development.yaml logs -f
docker compose -f compose.development.yaml exec <serviço> sh
dotnet build nextflow-erp.sln
dotnet test nextflow-erp.sln
dotnet ef migrations add <Nome> --project nextflow.Infrastructure --startup-project nextflow
```
No WSL o `dotnet` costuma estar em `~/.dotnet`, carregado pelo `~/.bashrc`: em shell não interativo exporte o `PATH`.
Se o build acusar `MSB3552` com `**/*.resx`, procure pastas com barra invertida no nome (sobra de build do Windows):
`find . -name '*\\*'`.

## IA
`AI_API_KEY` vazio = previsão só com o cálculo (status "Concluída sem IA"). A chave nunca vai para o repositório.
