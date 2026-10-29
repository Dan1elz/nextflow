namespace Nextflow.Domain.Dtos;

/// <summary>Item de combo (SearchSelect) quando a tela só precisa de valor e rótulo (`contracts/README.md`).</summary>
public class Option
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;

    public Option() { }

    public Option(string value, string label)
    {
        Value = value;
        Label = label;
    }
}
