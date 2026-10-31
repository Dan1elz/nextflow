namespace Nextflow.Domain.Dtos.Clients;

/// <summary>Cadastro rápido sem sair da venda (T-CD4): nome e telefone.</summary>
public class QuickCreateClientRequestDto
{
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
}
