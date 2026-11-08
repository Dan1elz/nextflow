namespace Nextflow.Application.Forecast;

public enum PromotionTrigger : byte
{
    None = 0,
    Excess = 1,    // excesso de estoque em baixa temporada
    Validity = 2   // validade próxima
}
