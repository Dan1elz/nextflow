# Níveis do domínio — Nextflow API

> **Origem:** DER consolidado de 28/10/2026 (`../nextflow-docs/docs/architecture/diagrams/der-consolidado.md`,
> `der-catalogo-v2-sazonalidade.md`, `der-catalogo-estoque.md`), no formato do `DOMAIN_LEVELS.md` do atrena-api.
> **Status:** rascunho; acompanha o DER. Mudou o DER, mude aqui.

Mapeamento das tabelas do domínio por nível de dependência (N).

O nível define a **ordem mínima de criação** de uma entidade em relação às suas dependências por FK. Uma entidade de nível
N só existe no banco depois que todas as dos níveis anteriores existem.

Usado como referência para: ordem de migrations, seeders (inclusive o seed demo), testes de integração e sequência de
implementação de repositórios e use cases.

---

## Legenda

| Símbolo | Significado |
|---|---|
| `→` | FK obrigatória (NOT NULL) |
| `→?` | FK opcional (nullable) |
| `⇄?` | FK circular nullable (constraint criada depois das duas tabelas) |
| `~>` | referência polimórfica (`owner_type` + `owner_id`, ou `source_type` + `source_id`), sem FK no banco |
| *(derivada)* | tabela recalculada a partir de outras; nunca editada pelo usuário |

---

## N1 — Entidades independentes

| Tabela (modelo) | Dependências |
|---|---|
| `countries` (`Country`) | nenhuma |
| `users` (`User`) | nenhuma |
| `clients` (`Client`) | nenhuma |
| `suppliers` (`Supplier`) | nenhuma |
| `categories` (`Category`) | `→?` `categories` (`parent_id`, autorreferência; nulo = nível 1) |
| `collections` (`Collection`) | nenhuma |
| `calendar_events` (`CalendarEvent`) | nenhuma |
| `stock_policies` (`StockPolicy`) | nenhuma (PK = classe ABC) |
| `stores` (`Store`) | `⇄?` `stock_locations` (`default_sale_location_id`, `default_receive_location_id`) |
| `forecast_runs` (`ForecastRun`) | nenhuma |

---

## N2 — Dependem de N1

| Tabela | Dependências |
|---|---|
| `states` | → `countries` |
| `stock_locations` | → `stores` |
| `user_stores` | → `users` · → `stores` |
| `products` | →? `collections` |
| `contacts` | `~>` `clients` ou `suppliers` |
| `notifications` | → `users` · `~>` origem (`source_type`, `source_id`) |
| `forecast_ai_batches` | → `forecast_runs` |
| `forecast_event_context` | → `forecast_runs` · → `calendar_events` |

---

## N3 — Dependem de N2 (e N1)

| Tabela | Dependências |
|---|---|
| `cities` | → `states` |
| `category_products` | → `categories` · → `products` |
| `product_events` | → `products` · → `calendar_events` |
| `product_suppliers` | → `products` · → `suppliers` |
| `product_packagings` | → `products` · →? `suppliers` |
| `product_attributes` | → `products` |
| `product_variants` | → `products` |
| `product_seasonality` *(derivada)* | → `products` |
| `product_sales_monthly` *(derivada)* | → `products` |
| `forecast_projections` | → `forecast_runs` · → `products` |
| `forecast_recommendations` | → `forecast_runs` · → `products` |

---

## N4 — Dependem de N3 (e abaixo)

| Tabela | Dependências |
|---|---|
| `addresses` | → `cities` · `~>` `clients` ou `suppliers` |
| `product_attribute_values` | → `product_attributes` |
| `price_history` | → `product_variants` · → `users` (`changed_by`) |
| `lots` | → `product_variants` |
| `stock_location_minimums` | → `product_variants` · → `stock_locations` |
| `promotions` | → `products` · →? `product_variants` (nulo = todas as variações) |
| `orders` | → `stores` · → `stock_locations` (`sale_location_id`) · →? `clients` (nulo = balcão) · → `users` |
| `purchase_orders` | → `stores` · → `stock_locations` (`receive_location_id`) · → `suppliers` |
| `stock_transfers` | → `stock_locations` (origem e destino) · → `users` |
| `inventory_counts` | → `stock_locations` · →? `categories` |
| `lost_demand_records` | → `product_variants` · → `stock_locations` · → `users` |
| `stock_daily_snapshots` *(derivada)* | → `product_variants` · → `stock_locations` |
| `forecast_accuracy` *(derivada)* | → `forecast_projections` |

---

## N5 — Dependem de N4 (e abaixo)

| Tabela | Dependências |
|---|---|
| `product_variant_values` | → `product_variants` · → `product_attribute_values` |
| `stock_balances` | → `product_variants` · → `stock_locations` · →? `lots` |
| `stock_movements` | → `product_variants` · → `stock_locations` · →? `lots` · → `users` · `~>` documento de origem |
| `order_items` | → `orders` · → `product_variants` · →? `lots` (preenchido na baixa, FEFO) · →? `promotions` |
| `payments` | → `orders` |
| `purchase_items` | → `purchase_orders` · → `product_variants` |
| `stock_transfer_items` | → `stock_transfers` · → `product_variants` · →? `lots` |
| `forecast_recommendation_items` | → `forecast_recommendations` · → `product_variants` · →? `stock_locations` (origem/destino de Transferir) |

---

## N6 — Dependem de N5

| Tabela | Dependências |
|---|---|
| `inventory_count_items` | → `inventory_counts` · → `product_variants` · →? `lots` · →? `stock_movements` (`adjustment_movement_id`) |

---

## Grafo resumido

```
N1:  countries   users   clients   suppliers   categories   collections   calendar_events   stores ⇄? ─┐   forecast_runs
        │          │         ~>        ~>            │            │                │             │       │        │
N2:   states   user_stores   contacts            products ◄──────┘                │       stock_locations  forecast_ai_batches
        │                                            │                             │             │
N3:   cities          product_variants · product_attributes · product_suppliers · category_products · product_events
        │                     │                │                                   forecast_recommendations · forecast_projections
N4:  addresses     lots · price_history · promotions · stock_location_minimums
                   orders · purchase_orders · stock_transfers · inventory_counts      product_attribute_values
                              │
N5:      stock_balances · stock_movements · order_items · payments · purchase_items · stock_transfer_items
         product_variant_values · forecast_recommendation_items
                              │
N6:                   inventory_count_items
```

---

## Circularidades e polimorfismo — detalhamento

### Par 1: `stores` ⇄ `stock_locations`

```
stores.default_sale_location_id?      →  stock_locations
stores.default_receive_location_id?   →  stock_locations
stock_locations.store_id              →  stores
```

**Fluxo real:** a loja é criada sem locais; o primeiro local ("Loja") é criado com `store_id` e gravado como padrão de venda
e de recebimento pelo método `Store.SetDefaults(saleLocationId, receiveLocationId)`, no mesmo commit do `CreateStoreUseCase`.

### Referências polimórficas (`~>`)

| Tabela | Colunas | Regra |
|---|---|---|
| `addresses`, `contacts` | `owner_type` (`Client`, `Supplier`) + `owner_id` | `CHECK` de `owner_type`; existência do dono validada em `ValidateBusinessRules` |
| `stock_movements` | `source_type` (`Order`, `PurchaseOrder`, `StockTransfer`, `InventoryCount`, `Adjustment`) + `source_id` | gravado só pelo `IStockLedger`, que recebe o documento tipado |
| `notifications` | `source_type` + `source_id` | gravado só pelo use case/job que cria a notificação |

---

## Inconsistências e pontos de atenção (antes da primeira migration)

### 1. `addresses` e `contacts` — associação polimórfica sem FK

**Problema:** o mesmo problema que o atrena registrou para `Address`: nada no banco garante que o dono existe.

**Sugestão:** manter o polimorfismo (é o DER validado), com `CHECK (owner_type IN (...))`, índice em `(owner_type, owner_id)`
e validação do dono no use case. Reavaliar se surgir terceiro tipo de dono.

### 2. `forecast_recommendations` ↔ documento aplicado — lado da FK indefinido

**Problema:** o DER desenha `forecast_recommendations |o--o| purchase_orders / promotions / stock_transfers`, sem dizer
onde fica a FK.

**Sugestão:** a recomendação guarda o que foi gerado (`applied_purchase_order_id?`, `applied_promotion_id?`,
`applied_transfer_id?`, todos nullable e preenchidos por `Recommendation.Apply(...)`), o que a coloca com FKs para N4.
Isso permite L-CP5 (cancelar a compra devolve a recomendação a Pendente). Confirmar com o Daniel.

### 3. `categories` — profundidade de 2 níveis só por regra

**Problema:** a autorreferência permite qualquer profundidade; RN-CE23 limita a 2.

**Sugestão:** validar em `ValidateBusinessRules` (pai não pode ter pai). Sem `CHECK` no banco.

### 4. Tabelas derivadas

`product_sales_monthly`, `product_seasonality`, `stock_daily_snapshots` e `forecast_accuracy` são recalculadas por job ou
pela análise. Não têm use case de escrita exposto na API e não entram no seed como dado digitado: o seed gera os movimentos
e roda o mesmo cálculo (RNF002, saldo = soma das movimentações).

### 5. `order_items.unit_cost` e `lot_id` — preenchidos na baixa

O item de venda congela o custo médio e o lote (FEFO) no momento em que o `IStockLedger` registra a saída, não na criação
do orçamento. O `Order.Pay(...)` recebe o resultado do ledger para preencher esses campos.

### 6. `sales` removida

A venda é o pedido pago (`orders.status = Paid`). Qualquer consulta antiga a `sales` vira consulta a `orders` + `payments`.

---

## Ordem de implementação recomendada

Para qualquer entidade nova ou refatoração, siga a sequência:

1. Definir/corrigir o modelo e seus value objects (Domain).
2. Criar/atualizar a `IEntityTypeConfiguration` correspondente.
3. Gerar a migration.
4. Criar/atualizar a interface do repositório em `Domain/Interfaces/Infrastructure/Repositories/`.
5. Implementar o repositório concreto na Infrastructure.
6. Registrar no DI (se não for por convenção).
7. Criar DTOs e validators (`Domain/Dtos/<Recurso>/`, `Domain/Validators/`).
8. Implementar os use cases (Create, Update, GetAll, GetById, Delete, Reactivate e os de negócio) sobre as bases.
9. Criar/atualizar o controller herdando `ApiController`.
10. Testes: unitários para regras, integração para repositório e fluxos.
11. Atualizar o `contract.md` do módulo se o DTO divergiu.

Sempre na ordem N1 → N6, agrupada pelas fases do plano:

| Fase | Tabelas |
|---|---|
| 0. Fundação | `countries`, `states`, `cities`, `users`, `clients`, `suppliers`, `addresses`, `contacts`, `stores`, `stock_locations`, `user_stores` |
| 1. Catálogo e estoque | `categories`, `collections`, `calendar_events`, `stock_policies`, `products` e filhas (N3), `product_attribute_values`, `product_variant_values`, `price_history`, `lots`, `stock_location_minimums`, `stock_balances`, `stock_movements`, depois `stock_transfers`/`inventory_counts` e itens |
| 2. Vendas e compras | `promotions`, `orders`, `order_items`, `payments`, `purchase_orders`, `purchase_items`, `lost_demand_records` |
| 3. Sazonalidade e previsão | tabelas derivadas, `forecast_*`, `notifications` |

Nunca crie um use case de N5 antes que os modelos de N1–N4 da mesma fase estejam estáveis.
