namespace SchemaComparison.Core.Models
{
    public class ColumnTypeMismatch
    {
        public string ColumnName { get; set; }
        public string DatabaseType { get; set; }
        public string EntityType { get; set; }
    }
}