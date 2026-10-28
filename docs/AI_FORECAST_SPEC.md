# Módulo de Previsão de Demanda com IA — Especificação Técnica (contrato)

> **Status:** implementado e validado ponta a ponta (etapa 4 — API real + frontend; branch `task-17` no backend e no frontend)
> **Data:** 11/10/2026
> Este documento é o **contrato único** entre backend e frontend. Nomes de entidades, campos, enums,
> rotas e formatos JSON aqui definidos são obrigatórios. Qualquer divergência é bug.

---

## 1. Objetivo

Prever a demanda dos **próximos 1 a 3 meses** por produto a partir do histórico de vendas (sazonalidade),
e transformar a previsão em **ações executáveis no dashboard**:

- **Comprar** → gera um Pedido de Compra (status Orçamento) pré-preenchido.
- **Promover** → cria uma Promoção com período (desconto % com início/fim) que altera o preço de venda enquanto vigente.
- **Descartar** → registra que o usuário ignorou a recomendação.

Princípio: **o backend calcula, a IA interpreta, o humano decide.** Números vêm de cálculo determinístico;
a IA (Gemini por enquanto, atrás de uma interface) prioriza e explica. Nada é executado sem clique do usuário.

---

## 2. Visão geral do fluxo

```
[Admin clica "Gerar análise"]
  → POST /api/forecasts/generate
     1. Rebuild de product_sales_monthly (agregado mensal de vendas confirmadas, últimos 36 meses)
     2. ForecastCalculator (determinístico): índice sazonal, demanda projetada, cobertura, sugestão de compra/promoção
     3. Recalcula product_seasonality (cache do perfil sazonal por produto)
     4. Monta payload JSON compacto → IAiClient (Gemini) com responseSchema
     5. Guardrails: valida/limita a resposta da IA contra o cálculo
        (se IA indisponível/falhar → usa só o cálculo, status CompletedWithoutAi)
     6. Persiste ForecastRun + ForecastRecommendation + ForecastProjection
     7. Recomendações Pendentes de runs anteriores → Expired
[Dashboard] GET /api/forecasts/latest → card "Recomendações da IA" com botões de ação
```

---

## 3. Modelo de dados

Convenções do projeto (seguir à risca): models em `nextflow.Domain/Models`, herdam `BaseModel`
(Id Guid + CreateAt UTC), `[Table("snake_case_plural")]`, setters privados, construtor a partir de DTO,
`Preposition/Singular/Plural` em pt-BR, enums `: byte` começando em 1. Uma **única migration** nova:
`AddAiForecastModule`.

### 3.1 Alterações em entidades existentes

**Product** (`products`)
| Campo | Tipo | Regra |
|--|--|--|
| `Brand` | `string?` (max 60) | Marca. Opcional. |
| `CostPrice` | `decimal?` | Custo de referência (último custo). Editável no cadastro; **atualizado automaticamente** quando um Pedido de Compra muda para `Received` (usa o `CostPrice` do item). |
| `MinStock` | `decimal` (default 0, ≥ 0) | Estoque mínimo/segurança definido pelo usuário. |
| `Promotions` | navegação `ICollection<Promotion>` | — |

- Adicionar em `CreateProductDto`/`UpdateProductDto`, `UpdateProductRequestDto`/`ProductRequestDto` (API, multipart), `ProductResponseDto`, construtor e `Update(dto)` do model, e método `UpdateCostPrice(decimal)`.
- `ProductResponseDto` ganha também:
  - `EffectivePrice` (`decimal`) — preço com promoção vigente aplicada (= `Price` se não houver).
  - `ActivePromotion` (`PromotionSummaryDto?`) — promoção vigente hoje, se houver.

**Supplier** (`suppliers`)
| Campo | Tipo | Regra |
|--|--|--|
| `LeadTimeDays` | `int` (default 7, 1–180) | Prazo de entrega padrão do fornecedor em dias. |

- Adicionar em Create/Update/Response DTOs e no model.

**PurchaseOrder** (`purchase_orders`)
| Campo | Tipo | Regra |
|--|--|--|
| `ReceivedAt` | `DateTime?` | Preenchido (UTC) quando o status muda para `Received`. Usado para medir o lead time real. |

- `PurchaseOrderResponseDto` ganha `ReceivedAt`.

**UserResponseDto** ganha `Role` (`RoleEnum`) — o front precisa saber se o usuário é Admin.

**Dashboard**: alerta de estoque passa a usar `p.Quantity < (p.MinStock > 0 ? p.MinStock : 15)`; `StockAlertDto` ganha `MinStock`.

### 3.2 Novas entidades

**Promotion** (`promotions`) — implementa `IDeletable`
| Campo | Tipo | Regra |
|--|--|--|
| `ProductId` | Guid (FK products) | obrigatório |
| `DiscountPercent` | decimal | 1 a 90 (percentual, ex.: 15 = 15%) |
| `StartDate` | DateOnly | obrigatório |
| `EndDate` | DateOnly | ≥ StartDate; período máx. 90 dias **inclusivo** (`EndDate − StartDate + 1 ≤ 90`) |
| `Reason` | string? (max 255) | motivo/observação |
| `Source` | `PromotionSource` | Manual=1, AiRecommendation=2 |
| `ForecastRecommendationId` | Guid? | quando criada a partir de recomendação |
| `CreatedByUserId` | Guid (FK users) | — |
| `IsActive` | bool | false = cancelada |
| `UpdateAt` | DateTime? | — |

Regras: na criação `StartDate >= hoje`; **não pode haver sobreposição** de período com outra promoção ativa do mesmo produto; produto deve existir e estar ativo. "Vigente" = `IsActive && StartDate <= hoje <= EndDate`.

**ProductSalesMonthly** (`product_sales_monthly`) — tabela de agregação (cache de cálculo; reconstruída a cada geração)
| Campo | Tipo |
|--|--|
| `ProductId` | Guid (FK) |
| `Year` | int |
| `Month` | int (1–12) |
| `QuantitySold` | decimal |
| `Revenue` | decimal (Σ UnitPrice×Quantity − Discount) |
| `OrdersCount` | int |
| `UpdatedAt` | DateTime |
Índice único `(ProductId, Year, Month)`.
**Fonte da verdade da demanda:** `OrderItems` cujo `Order.Status == PaymentConfirmed` e `Order.Type == Sale` **ou** orçamento efetivado (`Status == PaymentConfirmed`) — ou seja, basta `Status == PaymentConfirmed`. Data de referência = `Order.CreateAt` convertida para o fuso `America/Sao_Paulo`.

**ProductSeasonality** (`product_seasonality`) — perfil sazonal em cache (upsert a cada geração)
| Campo | Tipo |
|--|--|
| `ProductId` | Guid (FK) |
| `Month` | int (1–12) |
| `SeasonalIndex` | decimal (1.0 = média; 1.8 = 80% acima) |
| `MonthsOfHistory` | int |
| `UpdatedAt` | DateTime |
Índice único `(ProductId, Month)`.

**ForecastRun** (`forecast_runs`)
| Campo | Tipo |
|--|--|
| `RequestedByUserId` | Guid (FK users) |
| `HorizonMonths` | int (1–3) |
| `Status` | `ForecastRunStatus`: Completed=1, CompletedWithoutAi=2, Failed=3 |
| `AiProvider` | string? (ex.: "Gemini") |
| `AiModel` | string? |
| `Summary` | string? (text) — resumo geral em pt-BR (da IA ou template) |
| `ErrorMessage` | string? (text) — motivo do fallback/falha |
| `InputSnapshotJson` | string? (text) — payload enviado à IA (auditoria/TCC) |
| `DurationMs` | int |
| `ProductsAnalyzed` | int |

**ForecastRecommendation** (`forecast_recommendations`)
| Campo | Tipo |
|--|--|
| `ForecastRunId` | Guid (FK) |
| `ProductId` | Guid (FK) |
| `Action` | `RecommendationAction`: Buy=1, Promote=2, Hold=3 |
| `Priority` | `RecommendationPriority`: Low=1, Medium=2, High=3 |
| `Confidence` | `ForecastConfidence`: Low=1, Medium=2, High=3 |
| `Status` | `RecommendationStatus`: Pending=1, Applied=2, Dismissed=3, Expired=4 |
| `SuggestedQuantity` | decimal? (Buy) |
| `SuggestedDiscountPercent` | decimal? (Promote) |
| `SuggestedPromotionDays` | int? (Promote) |
| `Deadline` | DateOnly? (Buy: data-limite para comprar considerando lead time) |
| `Reason` | string (max 1000) — explicação pt-BR |
| `CurrentStock` | decimal — snapshot |
| `IncomingStock` | decimal — qtd em pedidos de compra abertos (Budget/Pending) |
| `ProjectedDemand` | decimal — soma da demanda projetada no horizonte |
| `NextMonthDemand` | decimal |
| `CoverageDays` | decimal? — dias que o estoque atual cobre |
| `NextMonthSeasonalIndex` | decimal |
| `AppliedAt` | DateTime? |
| `AppliedByUserId` | Guid? |
| `PurchaseOrderId` | Guid? (FK) |
| `PromotionId` | Guid? (FK) |
| `DismissReason` | string? (max 255) |

**ForecastProjection** (`forecast_projections`) — demanda projetada mês a mês (para gráficos)
| Campo | Tipo |
|--|--|
| `ForecastRunId` | Guid (FK) |
| `ProductId` | Guid (FK) |
| `Year` | int |
| `Month` | int |
| `ProjectedQuantity` | decimal |

### 3.3 Enums novos (Domain/Enums, `: byte`) — espelhar em `nextflow-front/src/types/enums.ts`

| Enum | Valores | Labels pt-BR (front) |
|--|--|--|
| `PromotionSource` | Manual=1, AiRecommendation=2 | Manual, Recomendação da IA |
| `ForecastRunStatus` | Completed=1, CompletedWithoutAi=2, Failed=3 | Concluída, Concluída sem IA, Falhou |
| `RecommendationAction` | Buy=1, Promote=2, Hold=3 | Comprar, Promover, Manter |
| `RecommendationPriority` | Low=1, Medium=2, High=3 | Baixa, Média, Alta |
| `ForecastConfidence` | Low=1, Medium=2, High=3 | Baixa, Média, Alta |
| `RecommendationStatus` | Pending=1, Applied=2, Dismissed=3, Expired=4 | Pendente, Aplicada, Descartada, Expirada |

Serialização: enums trafegam como **número** (padrão atual do projeto, Newtonsoft camelCase).

---

## 4. Preço efetivo (promoções)

- Serviço `IProductPricingService` (interface em `Domain/Interfaces/Utils`, implementação em `Application/Utils` ou `Application/Services`):
  - `decimal GetEffectivePrice(Product product, DateOnly date)` — usa `product.Promotions` carregadas.
  - `Promotion? GetActivePromotion(Product product, DateOnly date)`.
- "Hoje" de negócio: helper `BusinessClock.Today` = data atual em `America/Sao_Paulo` (`TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")`).
- `CreateOrderUseCase` e `UpdateOrderUseCase` passam a usar o preço efetivo em `SetPricing(...)` (carregar produtos com `Include(p => p.Promotions.Where(vigente))`).
- `GetAllProductsUseCase`/`GetProductByIdUseCase` incluem promoções vigentes para preencher `EffectivePrice`/`ActivePromotion`.
- `GetOrderByIdUseCase` também inclui as promoções vigentes dos produtos dos itens (o formulário de edição do pedido
  mostra `product.effectivePrice`, que é o preço que o `UpdateOrderUseCase` cobra).
- A API usa cultura **invariante** por requisição (`UseRequestLocalization`), para que o multipart de produtos
  (`Price`, `CostPrice`, `MinStock`, `Quantity` com ponto decimal, ex.: `"5.1"`) seja lido igual em qualquer servidor.
- O frontend **nunca** recalcula promoção: usa `effectivePrice` vindo da API.

---

## 5. Cálculo determinístico (`ForecastCalculator`)

Classe **pura** (sem EF, sem I/O) em `nextflow.Application/Forecast/ForecastCalculator.cs`, recebendo DTOs internos e retornando resultado por produto. Deve ser fácil de testar e explicar no TCC.

Entradas por produto: série mensal (últimos 24 meses **completos**, mês corrente excluído), estoque atual, estoque a chegar (itens de pedidos de compra Budget/Pending), `MinStock`, lead time (média real `ReceivedAt − CreateAt` dos últimos pedidos recebidos do fornecedor; fallback `Supplier.LeadTimeDays`), `Price`, `CostPrice`, `Validity`, promoção vigente, índice sazonal da categoria de referência (primeira categoria ativa do produto em ordem alfabética).

1. **Meses de histórico** = meses desde a primeira venda (máx. 24).
2. **Índice sazonal do produto** (se ≥ 12 meses): `idx[m] = média(qtd no mês-calendário m) / média mensal geral`. Meses sem venda contam como 0.
3. **Índice da categoria**: mesmo cálculo com a soma dos produtos da categoria.
4. **Índice final**: `w = min(1, meses/24)`; `idx = w·idxProduto + (1−w)·idxCategoria` (sem histórico de categoria → 1.0). Limitar a `[0.05, 5]`.
5. **Nível dessazonalizado**: média dos últimos 6 meses de `qtd / max(idx, 0.2)` (ou dos meses disponíveis).
   - **Regra 5b — produto sazonal sem vendas recentes** (ex.: panetone, que só vende nov–dez): se o produto tem
     **≥ 12 meses de histórico** e a soma das vendas dos **últimos 6 meses < 15%** das vendas dos **últimos 12 meses**,
     o nível dessazonalizado (≈ 0) é descartado e, para cada mês projetado, usa-se a venda do **mesmo mês do ano anterior**
     × **tendência anual** (`Σ últimos 12 meses ÷ Σ 12 meses anteriores`, limitada a `[0.8, 1.25]`; 1.0 sem dados no
     período anterior). O motivo registra "baseado no mesmo período do ano anterior" e o payload da IA traz
     `projecaoBaseadaNoAnoAnterior: true`. O índice sazonal continua sendo calculado/gravado normalmente.
6. **Projeção**: `proj[mês futuro] = nível × idx[mês]` para os meses do horizonte (a partir do próximo mês; o mês corrente usa a fração restante de dias).
7. **Confiança**: `<6 meses` → Low; `6–17` → Medium; `≥18` → High.
8. **Cobertura (dias)** = `estoque / (demanda do próximo mês / 30)`.
9. **Necessidade de compra** = demanda projetada em `(leadTime + 30 dias)` + estoque de segurança (`max(MinStock, 0.25 × demanda do próximo mês)`) − estoque − estoque a chegar. Arredondar para cima; `≤ 0` → sem compra. `Deadline` = hoje + max(0, coberturaDias − leadTime).
10. **Candidato a Promover** se (a) estoque > 1.5 × demanda do horizonte **e** índice médio do horizonte < 0.9 (baixa temporada), **ou** (b) `Validity` dentro de 60 dias e estoque > demanda projetada até a validade. Não promover se já houver promoção vigente.
11. **Desconto sugerido**: `maxDesc = CostPrice ? 1 − (CostPrice × 1.10) / Price : 0.30` (preserva 10% de margem); sugerido = `clamp(arred5(min(maxDesc, 0.25)), 5%, maxDesc)`; se `maxDesc < 5%` → não promover. Duração sugerida: 14–30 dias.
12. **Prioridade** (base): Buy com cobertura < leadTime → High; < leadTime+15 → Medium; senão Low. Promote por validade → High; por excesso → Medium.
13. Produto sem nenhuma venda no período → Hold, confiança Low (não enviar à IA).

Motivo de template de "Manter": só afirma que o estoque "atende a demanda projetada do horizonte" quando
`estoque + a chegar ≥ demanda do horizonte`; caso contrário informa que cobre a janela `prazo de entrega + 30 dias`
(base do passo 9), mas não todo o horizonte, e sugere reavaliar nas próximas análises.

---

## 6. Integração com IA

### 6.1 Abstração

```csharp
// nextflow.Domain/Interfaces/Utils/IAiClient.cs
public interface IAiClient
{
    bool IsConfigured { get; }
    string ProviderName { get; }   // "Gemini"
    string Model { get; }
    Task<string> GenerateJsonAsync(string systemPrompt, string userPrompt, object responseSchema, CancellationToken ct);
}
```

Implementação `GeminiAiClient` (`nextflow.Application/Services/Ai/`), registrada com `builder.Services.AddHttpClient<IAiClient, GeminiAiClient>()`.
- `POST https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent`
- Header `x-goog-api-key: {ApiKey}`
- Body: `systemInstruction`, `contents[{role:"user", parts:[{text}]}]`, `generationConfig { responseMimeType: "application/json", responseSchema, temperature: 0.2 }`
- Resposta: `candidates[0].content.parts[0].text`.
- Timeout configurável; qualquer erro → exceção tratada pelo use case (fallback).

### 6.2 Configuração

`appsettings`/variáveis de ambiente (seção `Ai`):
| Chave | Env (docker) | Default |
|--|--|--|
| `Ai:Provider` | `AI_PROVIDER` → `Ai__Provider` | `Gemini` |
| `Ai:ApiKey` | `AI_API_KEY` → `Ai__ApiKey` | vazio (→ modo sem IA) |
| `Ai:Model` | `AI_MODEL` → `Ai__Model` | `gemini-flash-latest` |
| `Ai:TimeoutSeconds` | `AI_TIMEOUT_SECONDS` → `Ai__TimeoutSeconds` | `60` |

`.env.example` documenta as variáveis (sem valor real). `compose.development.yaml` e `compose.staging.yaml` repassam `Ai__*`. A chave **nunca** é logada nem retornada pela API.

### 6.3 Payload enviado (máx. 40 produtos: candidatos Buy/Promote primeiro, depois maiores receitas)

```json
{
  "dataReferencia": "2026-10-11",
  "horizonteMeses": 3,
  "mesesDoHorizonte": ["2026-11", "2026-12", "2027-01"],
  "eventosComerciais": [{ "nome": "Black Friday", "data": "2026-11-27" }, { "nome": "Natal", "data": "2026-12-25" }],
  "produtos": [{
    "id": "guid", "nome": "Barra Ao Leite 90g", "marca": "Cacau Real", "categoria": "Barras",
    "estoqueAtual": 60, "estoqueAChegar": 0, "estoqueMinimo": 20, "leadTimeDias": 12,
    "preco": 8.9, "custo": 5.1, "validade": "2027-03-01", "promocaoVigente": null,
    "vendasUltimos12Meses": [{ "mes": "2025-10", "qtd": 110 }],
    "indiceSazonal": { "2026-11": 1.2, "2026-12": 1.8, "2027-01": 0.6 },
    "demandaProjetada": { "2026-11": 120, "2026-12": 180, "2027-01": 60 },
    "coberturaDias": 15, "mesesDeHistorico": 24, "confianca": "Alta",
    "sugestaoCalculada": { "acao": "Comprar", "quantidade": 240, "descontoPct": null, "prioridade": "Alta" }
  }]
}
```

Eventos comerciais (serviço `CommercialCalendar`, calculados por ano): Páscoa (algoritmo de Gauss/Meeus), Dia das Mães (2º domingo de maio), Dia dos Namorados (12/06), Dia dos Pais (2º domingo de agosto), Dia das Crianças (12/10), Black Friday (sexta após o 4º quinta de novembro), Natal (25/12). Enviar apenas os que caem no horizonte.

### 6.4 Resposta esperada (responseSchema)

```json
{
  "resumoGeral": "string (pt-BR, até 600 caracteres)",
  "recomendacoes": [{
    "produtoId": "guid",
    "acao": "Comprar | Promover | Manter",
    "prioridade": "Alta | Media | Baixa",
    "quantidade": 0,
    "descontoPct": 0,
    "diasPromocao": 0,
    "motivo": "string (pt-BR, até 400 caracteres, cite números)"
  }]
}
```

### 6.5 Guardrails (obrigatórios, aplicados no backend)

- `produtoId` desconhecido → ignorado.
- Produto sem item na resposta → usa o cálculo.
- `Comprar` com necessidade calculada = 0 → rebaixado para `Manter` (motivo registra o ajuste).
- Quantidade de compra limitada a `[0.5×, 1.5×]` da necessidade calculada.
- Desconto limitado a `[5%, maxDesc]`; dias de promoção a `[7, 45]`.
- Confiança **sempre** vem do cálculo (a IA não altera).
- Falha/timeout/JSON inválido/sem chave → recomendações 100% do cálculo com motivos em template pt-BR; `Status = CompletedWithoutAi`, `ErrorMessage` explica.

---

## 7. API (todas exigem `[Authorize]`; respostas no envelope `ApiResponse<T>` existente)

Rotas relacionadas já existentes: pedidos de compra em `/api/purchaseorders` (o front usa `PurchaseOrders`; rota
case-insensitive), pedidos de venda em `/api/orders`, produtos em `/api/products` (POST multipart, PUT JSON).
Erros usam o envelope do `GlobalExceptionMiddleware` (`Status`, `Message`, `Errors` em PascalCase); 403 de
`RoleAuthorize` vem sem corpo (o front mostra mensagem padrão de permissão).

### 7.1 Previsões — `ForecastsController`, rota `api/forecasts`

| Método | Rota | Permissão | Body | Retorno `data` |
|--|--|--|--|--|
| POST | `/api/forecasts/generate` | Admin | `{ "horizonMonths": 3 }` | `ForecastRunDto` |
| GET | `/api/forecasts/latest` | todos | — | `ForecastRunDto` ou `null` (status 200) |
| GET | `/api/forecasts` | todos | query `offset,limit` | `ApiResponseTable<ForecastRunSummaryDto>` |
| GET | `/api/forecasts/{id}` | todos | — | `ForecastRunDto` |
| GET | `/api/forecasts/products/{productId}` | todos | query `months=24` | `ProductForecastDto` |
| POST | `/api/forecasts/recommendations/purchase` | todos | `ApplyPurchaseRequestDto` | `ApplyPurchaseResultDto` |
| POST | `/api/forecasts/recommendations/{id}/promotion` | Admin | `ApplyPromotionRequestDto` | `ForecastRecommendationDto` |
| POST | `/api/forecasts/recommendations/{id}/dismiss` | todos | `{ "reason": "string?" }` | `ForecastRecommendationDto` |

**DTOs (JSON camelCase):**

```ts
ForecastRunSummaryDto {
  id, createAt, requestedByUserId, requestedByName, horizonMonths,
  status: ForecastRunStatus, aiProvider?, aiModel?, summary?, errorMessage?,
  durationMs, productsAnalyzed,
  counts: { buy: number, promote: number, hold: number, pending: number }  // pending = Comprar/Promover ainda pendentes (Manter não conta)
}
ForecastRunDto extends ForecastRunSummaryDto {
  recommendations: ForecastRecommendationDto[]   // ordenadas: Pending primeiro, depois prioridade desc, Buy/Promote antes de Hold
}
ForecastRecommendationDto {
  id, forecastRunId, productId,
  product: { id, name, productCode, brand?, unitType, price, effectivePrice, costPrice?, quantity, supplierId, supplierName },
  action, priority, confidence, status,
  suggestedQuantity?, suggestedDiscountPercent?, suggestedPromotionDays?, deadline?,
  reason, currentStock, incomingStock, projectedDemand, nextMonthDemand, coverageDays?, nextMonthSeasonalIndex,
  appliedAt?, purchaseOrderId?, promotionId?, dismissReason?
}
ProductForecastDto {
  productId, productName,
  history: [{ year, month, label: "MM/yyyy", quantitySold, revenue }],     // últimos N meses COMPLETOS (mês corrente fora), inclusive meses zerados
  // o gráfico do front preenche o mês corrente (vazio) entre o histórico e a projeção, que começa no próximo mês
  projection: [{ year, month, label, projectedQuantity }],                  // do último run que incluiu o produto (pode ser vazio)
  seasonality: [{ month: 1..12, seasonalIndex }],                           // de product_seasonality (pode ser vazio)
  latestRecommendation?: ForecastRecommendationDto
}
ApplyPurchaseRequestDto {
  items: [{ recommendationId, quantity, costPrice }],   // costPrice obrigatório > 0
  note?: string
}
ApplyPurchaseResultDto {
  purchaseOrders: PurchaseOrderResponseDto[],           // 1 pedido por fornecedor (agrupado)
  recommendations: ForecastRecommendationDto[]          // já marcadas Applied
}
ApplyPromotionRequestDto { discountPercent, startDate: "yyyy-MM-dd", endDate: "yyyy-MM-dd", reason? }
```

Regras de ação: só recomendações `Pending` podem ser aplicadas/descartadas (senão 400). Purchase exige `Action == Buy`; promotion exige `Action == Promote`. O Pedido de Compra é criado reutilizando `ICreateUseCase<CreatePurchaseOrderDto, PurchaseOrderResponseDto>` com `Note` = "Gerado a partir da recomendação da IA (análise de dd/MM/yyyy)" + note do usuário. A promoção é criada pelo mesmo use case de promoções (`Source = AiRecommendation`, `ForecastRecommendationId` preenchido).

### 7.2 Promoções — `PromotionsController`, rota `api/promotions`

| Método | Rota | Permissão | Body | Retorno `data` |
|--|--|--|--|--|
| POST | `/api/promotions` | Admin | `{ productId, discountPercent, startDate, endDate, reason? }` | `PromotionResponseDto` (201) |
| GET | `/api/promotions` | todos | query `offset,limit,filters` (`productId`, `status`=`active`\|`scheduled`\|`ended`\|`canceled`, `source`) | `ApiResponseTable<PromotionResponseDto>` |
| GET | `/api/promotions/{id}` | todos | — | `PromotionResponseDto` |
| DELETE | `/api/promotions/{id}` | Admin | — | 204 (cancela: `IsActive=false`) |

```ts
PromotionResponseDto {
  id, productId, product?: ProductResponseDto(sem promoções aninhadas),
  discountPercent, startDate, endDate, reason?, source: PromotionSource,
  forecastRecommendationId?, createdByUserId, isActive,
  status: "active" | "scheduled" | "ended" | "canceled",   // calculado
  originalPrice, promotionalPrice,                          // calculados com Product.Price atual
  createAt
}
PromotionSummaryDto { id, discountPercent, startDate, endDate, promotionalPrice }
```

---

## 8. Dados de demonstração (`Infrastructure/Seeders/Demo/`)

Loja fictícia de **doces e presentes** com 36 meses de operação, para demonstrar a previsão na banca.

- **Flag:** só roda com `Seed:Demo:Enabled = true` (env `Seed__Demo__Enabled`; padrão `false` no `appsettings.json`, `true` no `appsettings.Development.json` e nos compose de dev/staging). Idempotente: se existir a categoria "Doces e Presentes", não faz nada.
- **Reproduzível:** `new Random(42)` + data-âncora `Seed:Demo:AnchorDate` (env `Seed__Demo__AnchorDate`, `yyyy-MM-dd`; vazio = hoje no fuso de negócio). Mesma âncora ⇒ mesmos dados.
- **Peças:** `DemoStoreCatalog` (dados), `DemoDemandModel` (taxa diária esperada: sazonalidade mensal × eventos do `CommercialCalendar` × dia da semana × tendência), `DemoStoreSimulation` (operação dia a dia, só em memória), `DemoStoreSeeder` (grava em uma transação, em lotes de 500 pedidos) e `ProductImagesSeeder` (fotos).
- **Catálogo:** 5 fornecedores fictícios (lead time 7, 10, 12, 15 e 25 dias), 11 categorias, 48 SKUs com marcas fictícias e 14 perfis sazonais (barras, bombons, ovos de Páscoa, trufas, panetones, achocolatados, confeitaria, balas, doces juninos, biscoitos, cestas, kits de Dia das Mães/Namorados e cesta de Natal), 30 clientes (CPFs válidos gerados).
- **Simulação:** saldo de abertura, revisão de compra a cada 28 dias por fornecedor (necessidade = demanda esperada de lead time + 28 + 40 dias + `MinStock` − estoque − a chegar), recebimento com atraso aleatório, vendas limitadas ao estoque (ruptura = venda perdida) e devolução ao fornecedor da sobra de temporada. Pedidos com recebimento depois da âncora ficam `Pending` (estoque a chegar).
- **Espelha os fluxos reais (RNF002):** `Adjustment` "Saldo de abertura", `Entry` por item de compra recebida (com `ReceivedAt` e último custo em `CostPrice`), `Sales` por item vendido (`Quote` = preço unitário, mesma descrição do `CreateOrderUseCase`) e `Exit` na devolução de temporada. `Product.Quantity` = soma das movimentações.
- **Cenários (`DemoScenario`):** Comprar (última reposição não feita antes do pico), Promover por excesso (ovos encalhados da última Páscoa, achocolatado entrando no verão) e por validade (lote com validade em 45 dias), Manter, produto novo (lançado há 100 dias), em alta (+60% a.a.), em queda (−35% a.a.), ruptura histórica (110 dias sem reposição no meio do histórico) e 5 promoções passadas (Black Friday, queima de verão, Dia das Crianças, pós-Páscoa).
- **Volume (âncora 26/10/2026):** ~23 mil pedidos de venda (~50 mil itens), ~200 compras recebidas + 3 em trânsito; seed em ~30 s. Resultado da previsão de 3 meses: 12 Comprar, 4 Promover, 32 Manter.
- **Fotos:** `ProductImagesSeeder` baixa de `images.unsplash.com` (licença livre, id em `DemoProduct.PhotoId`) e sobe no storage. Só preenche produto do catálogo sem imagem; sem internet, o produto fica sem foto (`LogWarning`) e o startup segue.

---

## 9. Frontend

Padrões obrigatórios do projeto: Context → Provider → Hook → Component; services sobre `ApiService`/`BaseService`; shadcn/ui; Recharts; `formatCurrency`/`formatDateOnly`; `handleError`/`handleSuccess`; enums em `types/enums.ts` com labels pt-BR.

Arquivos novos:
- `src/interfaces/forecast.interface.ts`, `src/interfaces/promotion.interface.ts`
- `src/services/forecast.service.ts`, `src/services/promotion.service.ts`
- `src/contexts/forecast.context.tsx` + `src/providers/forecast.provider.tsx` + `src/hooks/use-forecast.tsx`
- `src/contexts/promotions.context.tsx` + `src/providers/promotions.provider.tsx` + `src/hooks/use-promotions.tsx`
- `src/components/app/ai-recommendations-card.tsx` — card do dashboard
- `src/components/app/apply-purchase-dialog.tsx`, `src/components/app/apply-promotion-dialog.tsx`, `src/components/app/forecast-chart.tsx`
- `src/pages/Forecast.tsx` (rota `/forecast`) — página "Previsão de Demanda (IA)"
- `src/pages/Promotions.tsx` (rota `/promotions`) — listagem de promoções com cancelamento

Alterações:
- `Home.tsx`: card "Recomendações da IA" entre os KPIs e o gráfico (top 5 Pending com Buy/Promote, por prioridade). Botões: **Comprar** (abre dialog com quantidade/custo editáveis → `POST recommendations/purchase`), **Aplicar desconto** (Admin; dialog com %/início/fim → `POST recommendations/{id}/promotion`), **Descartar**. Estado vazio com botão "Gerar análise" (Admin). Mostrar data da análise, status (badge "sem IA" quando `CompletedWithoutAi`) e link "Ver análise completa" → `/forecast`. Falha deste card não pode quebrar o dashboard.
- `Forecast.tsx`: cabeçalho com última análise + seletor de horizonte (1/2/3 meses) + botão "Gerar nova análise" (Admin, com loading — pode levar até ~60s); resumo da IA; tabs/filtros por ação (Comprar/Promover/Manter) e status; tabela de recomendações com seleção múltipla para "Comprar selecionados" (agrupa por fornecedor); ao selecionar um produto, gráfico de histórico (barras) + projeção (linha tracejada) + linha de estoque atual.
- Produto (form/view): campos **Marca**, **Custo**, **Estoque mínimo**; na visualização, badge de promoção vigente e nova aba "Previsão" (gráfico histórico + projeção + perfil sazonal 12 meses + promoções do produto).
- Fornecedor (form): campo **Prazo de entrega (dias)**.
- Listagem de produtos: badge "Promoção −X%" e preço promocional quando houver.
- `order-form.tsx`: usar `product.effectivePrice ?? product.price` para preço do carrinho/total; mostrar preço original riscado quando em promoção.
- `IUser` ganha `role?: TRoleEnum`; helper `isAdmin(user)` para esconder ações Admin.
- Sidebar: item "Previsão (IA)" (ícone `Sparkles` ou `BrainCircuit`) no grupo GERAL e "Promoções" (ícone `BadgePercent`) no grupo COMERCIAL.

---

## 10. Fora do escopo desta versão

- Previsão por loja/filial, por cliente ou por região.
- Treino de modelo de ML próprio (o cálculo é estatístico clássico; a IA generativa interpreta).
- Execução automática de compras/promoções sem confirmação humana.
- Job agendado (a geração é sob demanda pelo Admin).
