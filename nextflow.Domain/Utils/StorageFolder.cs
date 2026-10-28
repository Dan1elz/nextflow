using Nextflow.Domain.Enums;

namespace Nextflow.Domain.Utils;

/// <summary>
/// Onde um tipo de arquivo mora no storage: bucket + prefixo da chave.
/// Novo caso de uso (ex.: anexos de pedido de compra) = nova instância aqui
/// e, se for público, liberar o prefixo no <c>storage/init.sh</c>.
/// </summary>
public sealed record StorageFolder(StorageBucket Bucket, string Prefix)
{
    public static readonly StorageFolder ProductImages = new(StorageBucket.Public, "products");
}
