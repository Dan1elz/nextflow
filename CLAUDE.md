# CLAUDE.md

## Propósito

Este arquivo guarda o **contexto de produto e as regras inegociáveis** da API do **Nextflow**: o que vale para qualquer
conversa sobre o Nextflow, envolva código ou não. Responda e documente em português; código (nomes, classes, métodos) em inglês.

---

## Tarefas de desenvolvimento: leia os guias primeiro

> **Antes de qualquer trabalho de desenvolvimento** (implementar, refatorar, corrigir, revisar ou gerar código C#/.NET,
> desenhar um use case, controller ou repositório, escrever migration, ou qualquer coisa que toque o código), leia
> **primeiro** [`docs/dotnet-development-guidelines.md`](docs/dotnet-development-guidelines.md) e
> [`docs/C_SHARP_ARCHITECTURE_RULES.md`](docs/C_SHARP_ARCHITECTURE_RULES.md). Eles são a fonte de verdade de arquitetura,
> camadas, nomes, abstrações base, envelope de resposta, validação, persistência, segurança e testes. Não reimplemente
> essas regras de memória. Para a ordem das entidades, [`docs/DOMAIN_LEVELS.md`](docs/DOMAIN_LEVELS.md).

Para pedidos que não são de desenvolvimento (dúvida de produto, regra de negócio, documentação, planejamento), o contexto
abaixo e o `../nextflow-docs` bastam.

---

## Contexto do produto

O Nextflow é um **micro-ERP com inteligência de compras** para comércios de 4 a 15 funcionários (ADR 0001). É o TCC do
Daniel, com Ricardo e Henrique; a banca é em dezembro de 2026. O seed de demonstração é uma loja de doces e presentes.

O fluxo central é:

1. O Admin cadastra produtos (com variações), fornecedores, lojas e locais de estoque.
2. Compras entram no estoque pelo recebimento do pedido de compra, que atualiza o custo médio.
3. Vendas saem do estoque: venda rápida de balcão ou pedido com orçamento (validade, reserva de estoque).
4. Todo movimento de estoque fica no livro-razão; o saldo é sempre a soma dos movimentos.
5. Uma vez por semana (ou pelo botão), a análise de previsão projeta a demanda com sazonalidade, e a IA prioriza e explica.
6. O Admin aplica as recomendações: **Comprar** (gera pedido de compra em orçamento), **Promover** (cria promoção),
   **Transferir** (entre locais) ou **Manter**.

---

## Documentos do produto

Antes de implementar, leia o módulo em `../nextflow-docs/docs/architecture/modules/<módulo>/`:

```text
../nextflow-docs/docs/
  product-overview.md                     # visão e escopo
  architecture/architecture-overview.md   # HLD
  architecture/decisions/                 # ADRs globais (0001…0010)
  architecture/diagrams/der-consolidado.md
  architecture/contracts/README.md        # convenções front × back
  architecture/modules/<módulo>/
    overview.md  business-rules.md (RN-*)  contract.md (telas → DTOs)  decisions/
```

| Módulo | Pasta | Prefixo das regras |
|--|--|--|
| Acesso | `access` | RN-AC |
| Cadastros (clientes, fornecedores) | `registry` | RN-CD |
| Catálogo e estoque | `catalog-inventory` | RN-CE |
| Vendas | `sales` | RN-VD |
| Compras | `purchasing` | RN-CP |
| Inteligência de compras, painel e notificações | `demand-forecast` | RN-DF |

Se uma feature conflita com a documentação, não mude a regra em silêncio: pare e aponte o conflito. Regra marcada
"rascunho / a validar" ainda não é fechada.

---

## Termos do domínio

Identificadores em inglês; rótulos em português só na apresentação (front).

| Português | No código |
|--|--|
| Produto | Product |
| Variação (tamanho/cor) | ProductVariant |
| Atributo / valor da grade | ProductAttribute / ProductAttributeValue |
| Categoria / Coleção / Evento | Category / Collection / CalendarEvent |
| Loja / Local de estoque | Store / StockLocation |
| Saldo / Movimentação / Lote | StockBalance / StockMovement / Lot |
| Transferência / Inventário | StockTransfer / InventoryCount |
| Pedido (orçamento ou venda) | Order |
| Pagamento | Payment |
| Promoção | Promotion |
| Pedido de compra / Recebimento | PurchaseOrder / Receive |
| Cliente / Fornecedor | Client / Supplier |
| Análise / Recomendação / Projeção | ForecastRun / ForecastRecommendation / ForecastProjection |
| Notificação | Notification |

---

## Regras de negócio obrigatórias (resumo; a fonte é o `business-rules.md` de cada módulo)

### Estoque
- Saldo = soma das movimentações; toda alteração de saldo gera movimentação (RN-CE01, RNF002).
- Só o `IStockLedger` grava movimento e saldo; movimento nunca é editado nem apagado (RN-CE14, ADR 0007).
- Saldo por variação × local × lote (RN-CE09, ADR 0008, ADR 0009). Venda e compra apontam para a variação (RN-CE34).
- Estoque negativo bloqueado, salvo produto com "permite negativo" (RN-CE32).
- Disponível = físico − reservas de orçamentos válidos; reserva é calculada, o físico só baixa no pagamento (RN-CE08).
- Custo médio ponderado por variação, recalculado em cada entrada de compra (RN-CE31).

### Vendas
- Dois estados de negócio: orçamento (com validade, reserva estoque) e venda paga (RN-VD16). Status:
  `Budget`, `Paid`, `Canceled`, `Refunded`, `Expired`.
- Preço do item = preço efetivo calculado só no backend (RN-VD02); orçamento congela o preço do dia (RN-VD19).
- Desconto em R$ por item (RN-VD03). Soma dos pagamentos = total (RN-VD05). Sem fiado (RN-VD18). Sem boleto (RN-VD20).
- Reembolso: caminho único, só Admin, até 7 dias de `paidAt`, devolve o estoque (RN-VD08, RN-VD07).
- Promoção abaixo do custo exige confirmação (RN-VD13).

### Compras
- Pedido de compra: Orçamento → Pendente (enviado ao fornecedor, grava `sentAt`) → Recebido / Cancelado (RN-CP07, RN-CP08).
- Recebimento é do pedido inteiro, gera entrada por item no local de entrega e atualiza o custo médio (RN-CP02, RN-CP04).
- Pedido recebido não cancela (RN-CP03).

### Previsão
- O backend calcula, a IA só prioriza e explica (ADR 0005); sem IA a análise termina `CompletedWithoutAi` (RN-DF13).
- Análise semanal + manual, em segundo plano, uma por vez, com notificação ao fim (RN-DF18, RN-DF23).
- Recomendação por produto, rateada nas variações pela grade (RN-DF21). Produto "fora de linha" nunca recebe Comprar (RN-CE19).
- Aplicar Comprar gera pedido de compra em Orçamento por fornecedor; nada é enviado sozinho (RN-DF15).

### Acesso
- Só o perfil Administrador (ADR 0004). Usuário inativo não loga (RN-AC02). Credencial inválida tem mensagem genérica (RN-AC03).

---

## Regras inegociáveis do produto

- **NUNCA** altere saldo de estoque sem gerar movimentação no livro-razão.
- **NUNCA** edite ou apague movimentação, pagamento ou histórico de preço; corrija com lançamento contrário.
- **NUNCA** calcule preço, total, saldo ou status no front; a API devolve pronto.
- **NUNCA** envie pedido de compra ao fornecedor automaticamente a partir da IA.
- **NUNCA** deixe a IA alterar a confiança da recomendação nem passar dos limites de RN-DF12.
- **NUNCA** gere análises em loop para testar: cada chamada à IA consome cota.
- **NUNCA** logue segredo (`JwtSettings:Key`, `Ai:ApiKey`, `STORAGE_*`), senha ou CPF/CNPJ completo.
- **NUNCA** faça commit, push, merge ou comando em servidor (SSH, deploy na VM) sem aprovação do Daniel.

> Todas as regras de engenharia inegociáveis (bases de use case, `ResultDto<T>`, `AppDbContext` fora de use case e
> controller, controllers finos, `CancellationToken`, livro-razão pelo `IStockLedger` etc.) estão em
> [`docs/C_SHARP_ARCHITECTURE_RULES.md`](docs/C_SHARP_ARCHITECTURE_RULES.md).
