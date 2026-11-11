using System.Text.Json;

namespace Nextflow.Tests.Support;

/// <summary>
/// Contrato do que vai para a IA externa (LGPD, art. 6º, III): lista fechada de campos por payload e nomes que nunca podem
/// aparecer. Campo novo no payload quebra o teste de propósito, para ser revisto antes de sair do servidor.
/// </summary>
public static class AiPayloadContract
{
    public static readonly string[] BatchRoot = ["dataReferencia", "horizonteMeses", "mesesDoHorizonte", "eventosComerciais", "produtos"];

    public static readonly string[] Event = ["nome", "data", "vendaComecaASubirEm"];

    public static readonly string[] Product =
    [
        "id", "nome", "marca", "categoria", "classeAbc", "estoqueAtual", "estoqueAChegar", "estoqueMinimo", "leadTimeDias",
        "preco", "custo", "validade", "foraDeLinha", "foraDaEpoca", "eventosDoProduto", "promocaoVigente",
        "vendasUltimos12Meses", "indiceSazonal", "demandaProjetada", "coberturaDias", "mesesDeHistorico", "confianca",
        "projecaoBaseadaNoAnoAnterior", "descontoMaximoPct", "sugestaoCalculada"
    ];

    public static readonly string[] ActivePromotion = ["descontoPct", "fim"];

    public static readonly string[] MonthlySale = ["mes", "qtd"];

    public static readonly string[] CalculatedSuggestion = ["acao", "quantidade", "descontoPct", "diasPromocao", "prioridade", "dataLimiteCompra"];

    public static readonly string[] SummaryRoot =
    [
        "dataReferencia", "horizonteMeses", "mesesDoHorizonte", "eventosComerciais", "produtosAnalisados", "comprar",
        "comprarPrioridadeAlta", "promover", "manter", "principais"
    ];

    public static readonly string[] SummaryItem = ["nome", "acao", "prioridade", "quantidade", "descontoPct", "motivo"];

    public static readonly string[] OnDemandPurchase =
    [
        "acao", "produto", "loja", "local", "saldo", "minimo", "aCaminho", "vendaDiaria90Dias", "vendaDiaria30Dias",
        "prazoEntregaDias", "pedidoMinimo", "multiplo", "sugestaoCalculada", "quantidadeMinima", "quantidadeMaxima"
    ];

    public static readonly string[] OnDemandPromotion =
    [
        "acao", "produto", "loja", "lote", "quantidadeNoLote", "venceEm", "diasParaVencer", "vendaDiaria90Dias",
        "vendaDiaria30Dias", "vendaEsperadaAteVencer", "preco", "custoMedio", "descontoMaximoPct", "diasMinimos",
        "diasMaximos", "sugestaoCalculada"
    ];

    /// <summary>Trechos de nome de campo que denunciam dado pessoal de cliente, usuário ou fornecedor.</summary>
    private static readonly string[] PersonalDataHints =
    [
        "cliente", "client", "customer", "usuario", "user", "cpf", "cnpj", "documento", "email", "telefone", "phone",
        "celular", "endereco", "address", "cep", "nascimento", "birth", "senha", "password", "fornecedor", "supplier"
    ];

    /// <summary>JSON que vem depois da primeira linha do prompt (instrução curta + payload).</summary>
    public static JsonElement PayloadOf(string userPrompt)
        => JsonDocument.Parse(userPrompt[(userPrompt.IndexOf('\n') + 1)..]).RootElement.Clone();

    public static string[] Keys(JsonElement element) => [.. element.EnumerateObject().Select(p => p.Name)];

    /// <summary>Todos os nomes de campo do JSON, em qualquer nível.</summary>
    public static IEnumerable<string> AllKeys(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    yield return property.Name;
                    foreach (var key in AllKeys(property.Value))
                        yield return key;
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    foreach (var key in AllKeys(item))
                        yield return key;
                break;
        }
    }

    /// <summary>Campos que parecem dado pessoal. Os mapas por mês (<c>indiceSazonal</c>, <c>demandaProjetada</c>) têm só chaves "aaaa-mm".</summary>
    public static string[] PersonalDataKeys(JsonElement element) => [.. AllKeys(element)
        .Where(k => PersonalDataHints.Any(h => k.Contains(h, StringComparison.OrdinalIgnoreCase)))
        .Distinct()];
}
