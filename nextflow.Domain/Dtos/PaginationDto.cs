namespace Nextflow.Domain.Dtos;

public class PaginationDto<T>
{
    private PaginationDto() { }
    public List<T> Items { get; set; } = [];
    public int Total { get; set; }

    public static PaginationDto<T> Paginate(IEnumerable<T> items, int total)
    {
        return new PaginationDto<T>
        {
            Items = [.. items],
            Total = total
        };
    }

    public static PaginationDto<T> Empty() => new() { Items = [], Total = 0 };
}
