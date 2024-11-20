namespace SchemaComparison.Core.Models
{
    public class ComparisonResult
    {
        public List<string> TablesOnlyInDatabase { get; set; } = new();
        public List<string> TablesOnlyInEntities { get; set; } = new();
        public Dictionary<string, TableDifference> TableDifferences { get; set; } = new();
    }
}