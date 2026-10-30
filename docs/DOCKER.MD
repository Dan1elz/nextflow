# Documentação de Execução: Backend Docker

### 🚨 Pré-requisito Global (Executar na primeira vez)

Antes de rodar qualquer outro comando, é necessário verificar a existência da rede externa compartilhada entre os projetos, permitindo a comunicação entre Backend, Frontend e Banco de Dados.

```bash
# Para ambiente de Desenvolvimento
docker network inspect nextflow-development-network >/dev/null 2>&1 || docker network create nextflow-development-network 

# Para ambiente de Homologação
docker network inspect nextflow-staging-network >/dev/null 2>&1 || docker network create nextflow-staging-network 

# Homologação na VM: rede do proxy de borda (~/projects/nginx)
docker network inspect proxy-net >/dev/null 2>&1 || docker network create proxy-net
```

> ⚠️ **Observação (Windows):** a mensagem de erro relacionada a `/dev/null` pode aparecer, mas a criação da rede ocorre normalmente.

---

## 🏛 Motivação da Arquitetura

A arquitetura do backend segue o mesmo princípio adotado no frontend:
**a própria aplicação é responsável pelo seu build e deploy**, garantindo previsibilidade entre ambientes e evitando dependência de ferramentas externas complexas.

A estrutura baseia-se nos seguintes arquivos:

* **Dockerfile:** Responsável por gerar a imagem do backend pronta para execução (produção/homologação).
* **Dockerfile.development:** Imagem voltada para desenvolvimento local, com hot reload e ferramentas adicionais.
* **compose.staging.yaml:** Orquestração do backend e dependências para ambientes de homologação/produção.
* **compose.development.yaml:** Orquestração para ambiente de desenvolvimento local.

---

## 1. Ambiente de Desenvolvimento (Local)

Este ambiente habilita:

* **Hot Reload** da aplicação backend.
* Uso de **variáveis locais** via `.env`.
* Comunicação direta com banco de dados via Docker Network.

**Pré-requisito:**
Certifique-se de possuir um arquivo `.env` na raiz do backend (copie do `.env.example`).

Para iniciar:

```bash
docker compose -f compose.development.yaml up -d --build
```

> **Acesso:**
> API em `http://localhost:8080`, Postgres em `localhost:5432`, storage S3 em `http://localhost:9000` (imagens) e painel do storage em `http://localhost:9001` (login `STORAGE_ROOT_USER` / `STORAGE_ROOT_PASSWORD`), todos publicados só em `127.0.0.1` (não são alcançáveis de fora da máquina).
>
> O `storage-initial-setup` roda a cada `up`, cria buckets e usuário da API e encerra: `Exited (0)` com o log terminando em **"Storage pronto."** é o normal. O backend só sobe depois dele.

---

## 2. Ambiente de Homologação

O ambiente de homologação é **idêntico ao de produção**, simulando o comportamento real de um pipeline de CI/CD.
As variáveis sensíveis são injetadas **no momento da execução**, garantindo isolamento e segurança.

### Comando de subida (Windows)

```bash
docker network create nextflow-staging-network & set DB_TIMEZONE=America/Sao_Paulo & set POSTGRES_DB=nextflow_staging_db & set POSTGRES_USER=admin_staging & set POSTGRES_PASSWORD=senha_segura_staging_123 & set JWT_KEY=chave_jwt_muito_segura_para_staging_32_chars & set SEED_ADMIN_PASSWORD=defina_a_senha_do_admin & set STORAGE_ROOT_USER=nextflow-admin & set STORAGE_ROOT_PASSWORD=senha_admin_storage_staging & set STORAGE_ACCESS_KEY=nextflow-api & set STORAGE_SECRET_KEY=senha_api_storage_staging & set STORAGE_PUBLIC_URL=https://nextflow-s3.zanni.tech & set CORS_ORIGINS=https://nextflow.zanni.tech & set POSTGRES_PORT=5434 & docker compose -f compose.staging.yaml up -d --build

```

### Variáveis utilizadas

| Variável            | Descrição                   |
| ------------------- | --------------------------- |
| `DB_TIMEZONE`       | Timezone do banco de dados  |
| `POSTGRES_DB`       | Nome do banco               |
| `POSTGRES_USER`     | Usuário do PostgreSQL       |
| `POSTGRES_PASSWORD` | Senha do banco              |
| `JWT_KEY`           | Chave de assinatura JWT     |
| `SEED_ADMIN_PASSWORD` | Senha do primeiro Admin (obrigatória no staging) |
| `POSTGRES_PORT`     | Porta do PostgreSQL no `127.0.0.1` |
| `STORAGE_ROOT_USER` / `STORAGE_ROOT_PASSWORD` | **Obrigatórias.** Admin do storage (senha com 8+ caracteres). Só o `storage` e o `storage-initial-setup` usam |
| `STORAGE_ACCESS_KEY` / `STORAGE_SECRET_KEY` | **Obrigatórias.** Usuário que a API usa, criado pelo `storage/init.sh` com acesso só aos buckets |
| `STORAGE_PUBLIC_URL` | **Obrigatória.** Endereço público do storage, usado nas URLs das imagens e na assinatura de URLs. Na VM: `https://nextflow-s3.zanni.tech` (exato, sem porta) |
| `STORAGE_BUCKET_PUBLIC` / `STORAGE_BUCKET_PRIVATE` | Nomes dos buckets (padrão `nextflow-public` / `nextflow-private`) |
| `STORAGE_CONSOLE_PORT` | Porta do painel do storage no `127.0.0.1` (padrão `9001`; de fora, túnel SSH) |
| `SEED_DEMO_ENABLED` | Liga o seed de demonstração (padrão `true` nos compose) → `Seed__Demo__Enabled` |
| `SEED_DEMO_ANCHOR_DATE` | Data-âncora da simulação (`yyyy-MM-dd`; vazio = hoje) → `Seed__Demo__AnchorDate` |
| `CORS_ORIGINS`      | Origens do front liberadas no CORS, separadas por vírgula (vazio = qualquer). Na VM: `https://nextflow.zanni.tech` |
| `AI_PROVIDER`       | Provedor de IA da previsão de demanda (default `Gemini`) → `Ai__Provider` |
| `AI_API_KEY`        | Chave da API de IA (vazio = previsão só com o cálculo, status "Concluída sem IA") → `Ai__ApiKey` |
| `AI_MODEL`          | Modelo (default `gemini-flash-latest`) → `Ai__Model` |
| `AI_TIMEOUT_SECONDS`| Tempo máximo da chamada à IA em segundos (default `60`) → `Ai__TimeoutSeconds` |

> As variáveis `AI_*` são opcionais e valem também para o ambiente de desenvolvimento (lidas do `.env`).
> A chave de IA nunca deve ser versionada nem logada.

> **Acesso:**
> A API **não publica porta**: fica na rede `proxy-net` com o alias `nextflow-api`, e o proxy de borda da VM a entrega em [https://nextflow-api.zanni.tech](https://nextflow-api.zanni.tech). Health check: `/health`.

### Atrás do proxy

* O container escuta só HTTP (8080); o HTTPS termina no proxy. O `UseForwardedHeaders` (primeiro middleware do `Program.cs`) lê `X-Forwarded-For`/`X-Forwarded-Proto` vindos de rede privada, então a aplicação enxerga o IP do visitante e `Request.Scheme = https`.
* As imagens ficam no storage (volume `nextflow_storage_data`). O `storage` entra na `proxy-net` com o alias `nextflow-s3`: o proxy de borda precisa de um site `nextflow-s3.zanni.tech` → `nextflow-s3:9000`, repassando host e caminho intactos (a URL assinada depende deles).

---

## 🔧 Gerenciamento e Debug

### Visualizar Logs

Útil para diagnosticar falhas de inicialização, migrations ou erros de API:

```bash
# Desenvolvimento
docker compose -f compose.development.yaml logs -f backend

# Homologação
docker compose -f compose.staging.yaml logs -f backend
```

---

### Acessar o Terminal do Container

Permite executar comandos internos, verificar arquivos ou testar conexões:

```bash
# Desenvolvimento
docker exec -it -e TERM=xterm nextflow-development-backend-1 sh

# Homologação
docker exec -it -e TERM=xterm nextflow-staging-backend-1 sh
```

---

### Parar o Ambiente

```bash
docker compose -f compose.development.yaml down
```

*(Nota: O nome do container pode variar de acordo com a pasta raiz do projeto. Caso falhe, utilize `docker ps` para identificar o nome correto.)*

---

## ✅ Considerações Importantes

* Quem chama a API é o **navegador** (pelo hostname público), não o container do front: o front não precisa estar na rede do backend.
* Variáveis de ambiente **não devem ser versionadas**.
* Ambientes de homologação e produção **devem sempre usar imagens buildadas**, nunca hot reload.
* Essa arquitetura é totalmente compatível com pipelines de **Bitbucket Pipelines / GitHub Actions**.

