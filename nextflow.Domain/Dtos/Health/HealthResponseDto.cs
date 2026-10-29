namespace Nextflow.Domain.Dtos.Health;

public class HealthResponseDto
{
    public string Status { get; set; } = string.Empty;
    public bool Database { get; set; }
    public DateTime CheckedAt { get; set; }

    public HealthResponseDto() { }

    public HealthResponseDto(bool database, DateTime checkedAt)
    {
        Status = database ? "Healthy" : "Degraded";
        Database = database;
        CheckedAt = checkedAt;
    }
}
