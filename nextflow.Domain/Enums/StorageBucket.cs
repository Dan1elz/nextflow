namespace Nextflow.Domain.Enums;

/// <summary>
/// Bucket do storage S3. Cada valor mapeia para um nome real em <c>Storage:Buckets</c>.
/// Uso interno da API (não é exposto ao front).
/// </summary>
public enum StorageBucket : byte
{
    /// <summary>Leitura anônima por prefixo (ex.: imagens de produtos). Acesso pela URL pública.</summary>
    Public = 1,

    /// <summary>Sem acesso anônimo (ex.: documentos). Acesso por URL assinada com validade curta.</summary>
    Private = 2
}
