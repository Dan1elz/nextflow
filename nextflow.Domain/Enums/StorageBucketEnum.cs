namespace Nextflow.Domain.Enums;

/// <summary>Bucket do storage S3 (ADR 0006). Cada valor mapeia para um nome em <c>Storage:Buckets</c>. Uso interno da API.</summary>
public enum StorageBucketEnum : byte
{
    /// <summary>Leitura anônima por prefixo (imagens de produto). Acesso pela URL pública.</summary>
    Public = 1,

    /// <summary>Sem acesso anônimo. Acesso por URL assinada com validade curta.</summary>
    Private = 2,
}
