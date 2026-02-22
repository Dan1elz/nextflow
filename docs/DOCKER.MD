# Documentação de Execução: Backend Docker

### 🚨 Pré-requisito Global (Executar na primeira vez)

Antes de rodar qualquer outro comando, é necessário verificar a existência da rede externa compartilhada entre os projetos, permitindo a comunicação entre Backend, Frontend e Banco de Dados.

```bash
# Para ambiente de Desenvolvimento
docker network inspect nextflow-development-network >/dev/null 2>&1 || docker network create nextflow-development-network 

# Para ambiente de Homologação
docker network inspect nextflow-staging-network >/dev/null 2>&1 || docker network create nextflow-staging-network 
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
> API disponível na porta configurada no `.env` (ex: `http://localhost:8080`)

---

## 2. Ambiente de Homologação

O ambiente de homologação é **idêntico ao de produção**, simulando o comportamento real de um pipeline de CI/CD.
As variáveis sensíveis são injetadas **no momento da execução**, garantindo isolamento e segurança.

### Comando de subida (Windows)

```bash
docker network create nextflow-staging-network & set DB_TIMEZONE=America/Sao_Paulo & set POSTGRES_DB=nextflow_staging_db & set POSTGRES_USER=admin_staging & set POSTGRES_PASSWORD=senha_segura_staging_123 & set JWT_KEY=chave_jwt_muito_segura_para_staging_32_chars & set BACKEND_PORT=8082 & set POSTGRES_PORT=5434 & docker compose -f compose.staging.yaml up -d --build

```

### Variáveis utilizadas

| Variável            | Descrição                   |
| ------------------- | --------------------------- |
| `DB_TIMEZONE`       | Timezone do banco de dados  |
| `POSTGRES_DB`       | Nome do banco               |
| `POSTGRES_USER`     | Usuário do PostgreSQL       |
| `POSTGRES_PASSWORD` | Senha do banco              |
| `JWT_KEY`           | Chave de assinatura JWT     |
| `BACKEND_PORT`      | Porta exposta pela API      |
| `POSTGRES_PORT`     | Porta externa do PostgreSQL |

> **Acesso:**
> API disponível em: [http://localhost:8081](http://localhost:8081)

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

* O backend e o frontend **devem estar na mesma network Docker** para comunicação direta.
* Variáveis de ambiente **não devem ser versionadas**.
* Ambientes de homologação e produção **devem sempre usar imagens buildadas**, nunca hot reload.
* Essa arquitetura é totalmente compatível com pipelines de **Bitbucket Pipelines / GitHub Actions**.

