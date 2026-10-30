using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Nextflow.Domain.ValueObjects;

namespace Nextflow.Infrastructure.Database.Converters;

public static class ValueObjectConverters
{
    /// <summary>CPF/CNPJ gravado só com dígitos; a leitura não revalida (o dado já entrou validado).</summary>
    public static readonly ValueConverter<TaxDocument, string> TaxDocument = new(
        v => v.Value,
        v => Domain.ValueObjects.TaxDocument.FromStored(v));
}
