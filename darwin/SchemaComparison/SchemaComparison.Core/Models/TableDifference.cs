namespace SchemaComparison.Core.Models
{
    public class TableDifference
    {
        public List<string> ColumnsOnlyInDatabase { get; set; } = new();
        public List<string> ColumnsOnlyInEntity { get; set; } = new();
        public List<ColumnTypeMismatch> TypeMismatches { get; set; } = new();
    }
}