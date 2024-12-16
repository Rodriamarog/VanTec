namespace SchemaComparison.Core.Models
{
public class ColumnTypeMismatch
{
    public string ColumnName { get; set; } = string.Empty;
    public string DatabaseType { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
}
}