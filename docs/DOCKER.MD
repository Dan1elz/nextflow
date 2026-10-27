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
> API em `http://localhost:8080` e Postgres em `localhost:5432`, publicados só em `127.0.0.1` (não são alcançáveis de fora da máquina).

---

## 2. Ambiente de Homologação

O ambiente de homologação é **idêntico ao de produção**, simulando o comportamento real de um pipeline de CI/CD.
As variáveis sensíveis são injetadas **no momento da execução**, garantindo isolamento e segurança.

### Comando de subida (Windows)

```bash
docker network create nextflow-staging-network & set DB_TIMEZONE=America/Sao_Paulo & set POSTGRES_DB=nextflow_staging_db & set POSTGRES_USER=admin_staging & set POSTGRES_PASSWORD=senha_segura_staging_123 & set JWT_KEY=chave_jwt_muito_segura_para_staging_32_chars & set PUBLIC_API_URL=https://nextflow-api.zanni.tech & set CORS_ORIGINS=https://nextflow.zanni.tech & set POSTGRES_PORT=5434 & docker compose -f compose.staging.yaml up -d --build

```

### Variáveis utilizadas

| Variável            | Descrição                   |
| ------------------- | --------------------------- |
| `DB_TIMEZONE`       | Timezone do banco de dados  |
| `POSTGRES_DB`       | Nome do banco               |
| `POSTGRES_USER`     | Usuário do PostgreSQL       |
| `POSTGRES_PASSWORD` | Senha do banco              |
| `JWT_KEY`           | Chave de assinatura JWT     |
| `POSTGRES_PORT`     | Porta do PostgreSQL no `127.0.0.1` |
| `PUBLIC_API_URL`    | **Obrigatória.** Endereço público da API; monta a URL absoluta das imagens dos produtos (`Storage__BaseUrl`). Na VM: `https://nextflow-api.zanni.tech` |
| `CORS_ORIGINS`      | Origens do front liberadas no CORS, separadas por vírgula (vazio = qualquer). Na VM: `https://nextflow.zanni.tech` |

> **Acesso:**
> A API **não publica porta**: fica na rede `proxy-net` com o alias `nextflow-api`, e o proxy de borda da VM a entrega em [https://nextflow-api.zanni.tech](https://nextflow-api.zanni.tech). Health check: `/health`.

### Atrás do proxy

* O container escuta só HTTP (8080); o HTTPS termina no proxy. O `UseForwardedHeaders` (primeiro middleware do `Program.cs`) lê `X-Forwarded-For`/`X-Forwarded-Proto` vindos de rede privada, então a aplicação enxerga o IP do visitante e `Request.Scheme = https`.
* As imagens enviadas ficam no volume `nextflow_assets` (`/app/assets`): recriar o container não apaga mais.

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

