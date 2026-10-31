namespace Nextflow.Domain.Dtos.Stores;

/// <summary>Campos que a edição aceita: o local não muda de loja.</summary>
public class UpdateLocationRequestDto
{
    public string Name { get; set; } = string.Empty;
    public bool IsSellable { get; set; } = true;
}
