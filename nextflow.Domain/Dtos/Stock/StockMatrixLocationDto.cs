using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.Stock;

public class StockMatrixLocationDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;

    public StockMatrixLocationDto() { }

    public StockMatrixLocationDto(StockLocation entity)
    {
        Id = entity.Id;
        Name = entity.Name;
        StoreName = entity.Store != null ? entity.Store.Name : string.Empty;
    }
}
