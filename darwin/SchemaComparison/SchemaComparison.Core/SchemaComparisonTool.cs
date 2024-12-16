using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;

namespace SchemaComparison.Core
{
    public class SchemaComparisonTool
    {
        private readonly string _connectionString;
        private readonly string _entityFilesPath;
        
        public SchemaComparisonTool(string connectionString, string entityFilesPath)
        {
            _connectionString = connectionString;
            _entityFilesPath = entityFilesPath;
        }

        public class ComparisonResult
        {
            public List<string> TablesOnlyInDatabase { get; set; } = new();
            public List<string> TablesOnlyInFiles { get; set; } = new();
            public Dictionary<string, TableDifference> TableDifferences { get; set; } = new();
            public int TotalTablesInDB { get; set; }
            public int TablesProcessed { get; set; }
            public int FilesFound { get; set; }
        }

        public class TableDifference
        {
            public List<string> ColumnsOnlyInDatabase { get; set; } = new();
            public List<string> ColumnsOnlyInFiles { get; set; } = new();
        }

        private int GetTotalVTTables()
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            var query = @"
                SELECT COUNT(*) 
                FROM sys.tables 
                WHERE name LIKE 'VT[_]%'";

            using var command = new SqlCommand(query, connection);
            return (int)command.ExecuteScalar();
        }

        public ComparisonResult CompareSchemas()
        {
            var result = new ComparisonResult();
            
            result.TotalTablesInDB = GetTotalVTTables();
            Console.WriteLine($"Total de tablas VT_ en la base de datos: {result.TotalTablesInDB}");
            
            var databaseTables = GetDatabaseSchema();
            result.TablesProcessed = databaseTables.Count;
            Console.WriteLine($"Tablas VT_ recuperadas para comparación: {result.TablesProcessed}");
            
            var entityFiles = ParseEntityFiles();
            result.FilesFound = entityFiles.Count;
            Console.WriteLine($"Archivos VT_*.cs encontrados: {result.FilesFound}");

            result.TablesOnlyInDatabase = databaseTables.Keys
                .Except(entityFiles.Keys)
                .ToList();

            result.TablesOnlyInFiles = entityFiles.Keys
                .Except(databaseTables.Keys)
                .ToList();

            foreach (var tableName in databaseTables.Keys.Intersect(entityFiles.Keys))
            {
                var dbColumns = databaseTables[tableName];
                var fileColumns = entityFiles[tableName];
                var difference = new TableDifference
                {
                    ColumnsOnlyInDatabase = dbColumns.Keys
                        .Except(fileColumns.Keys)
                        .ToList(),
                    ColumnsOnlyInFiles = fileColumns.Keys
                        .Except(dbColumns.Keys)
                        .ToList()
                };

                if (difference.ColumnsOnlyInDatabase.Any() || 
                    difference.ColumnsOnlyInFiles.Any())
                {
                    result.TableDifferences[tableName] = difference;
                }
            }

            return result;
        }

        private Dictionary<string, Dictionary<string, string>> GetDatabaseSchema()
        {
            var schema = new Dictionary<string, Dictionary<string, string>>();
            
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
                
            var query = @"
                SELECT 
                    REPLACE(t.name, 'VT_', '') AS TableName,
                    c.name AS ColumnName
                FROM sys.tables t
                INNER JOIN sys.columns c ON t.object_id = c.object_id
                WHERE t.name LIKE 'VT[_]%'
                ORDER BY t.name, c.column_id";

            using var command = new SqlCommand(query, connection);
            using var reader = command.ExecuteReader();
            
            while (reader.Read())
            {
                var tableName = reader["TableName"].ToString() ?? string.Empty;
                var columnName = reader["ColumnName"].ToString() ?? string.Empty;

                if (!schema.TryGetValue(tableName, out var columns))
                {
                    columns = new Dictionary<string, string>();
                    schema[tableName] = columns;
                }

                columns[columnName] = string.Empty;
            }

            return schema;
        }

        private Dictionary<string, Dictionary<string, string>> ParseEntityFiles()
        {
            var entities = new Dictionary<string, Dictionary<string, string>>();
            var files = Directory.GetFiles(_entityFilesPath, "VT_*.cs");

            foreach (var file in files)
            {
                var content = File.ReadAllText(file);
                var entity = ParseEntityClass(content);
                if (entity != null)
                {
                    var tableName = entity.Name.Replace("VT_", "");
                    entities[tableName] = entity.Properties;
                }
            }

            return entities;
        }

        private class EntityClass
        {
            public required string Name { get; set; }
            public Dictionary<string, string> Properties { get; set; } = new();
        }

        private EntityClass? ParseEntityClass(string content)
        {
            var classNameMatch = Regex.Match(content, @"public\s+(?:partial\s+)?class\s+(\w+)");
            if (!classNameMatch.Success) return null;

            var entity = new EntityClass
            {
                Name = classNameMatch.Groups[1].Value
            };

            var propertyPattern = @"public\s+([^\s]+)\s+(\w+)\s*{\s*get;\s*set;\s*}";
            var matches = Regex.Matches(content, propertyPattern);

            foreach (Match match in matches)
            {
                var propertyType = match.Groups[1].Value;
                var propertyName = match.Groups[2].Value;
                
                if (!propertyType.StartsWith("VT_") && !propertyType.Contains("ICollection"))
                {
                    entity.Properties[propertyName] = string.Empty;
                }
            }

            return entity;
        }

        private IEnumerable<string> GetAllTableNames(ComparisonResult result)
        {
            var allTables = new HashSet<string>();
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            
            var query = @"
                SELECT REPLACE(name, 'VT_', '') AS TableName
                FROM sys.tables
                WHERE name LIKE 'VT[_]%'
                ORDER BY name";

            using var command = new SqlCommand(query, connection);
            using var reader = command.ExecuteReader();
            
            while (reader.Read())
            {
                var tableName = reader["TableName"].ToString();
                if (!string.IsNullOrEmpty(tableName))
                {
                    allTables.Add(tableName);
                }
            }
            
            return allTables;
        }

        private bool IsTablePerfectMatch(string tableName, ComparisonResult result)
        {
            return !result.TablesOnlyInDatabase.Contains(tableName) &&
                   !result.TablesOnlyInFiles.Contains(tableName) &&
                   !result.TableDifferences.ContainsKey(tableName);
        }

        public string GenerateReport(ComparisonResult result)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Reporte de Comparación entre Base de Datos y Archivos Entity Framework");
            sb.AppendLine("=============================================================\n");

            sb.AppendLine("Diagnóstico:");
            sb.AppendLine($"• Total de tablas VT_ en la base de datos: {result.TotalTablesInDB}");
            sb.AppendLine($"• Tablas VT_ procesadas: {result.TablesProcessed}");
            sb.AppendLine($"• Archivos VT_*.cs encontrados: {result.FilesFound}\n");

            var allTables = GetAllTableNames(result).OrderBy(t => t);
            foreach (var tableName in allTables)
            {
                if (IsTablePerfectMatch(tableName, result))
                {
                    sb.AppendLine($"✅ VT_{tableName} - Tabla congruente");
                }
                else
                {
                    sb.AppendLine($"❌ VT_{tableName} - Incongruencias encontradas:");

                    if (result.TablesOnlyInDatabase.Contains(tableName))
                    {
                        sb.AppendLine($"   • Tabla existe en BD pero no en archivo");
                        continue;
                    }

                    if (result.TablesOnlyInFiles.Contains(tableName))
                    {
                        sb.AppendLine($"   • Tabla existe en archivo pero no en BD");
                        continue;
                    }

                    if (result.TableDifferences.ContainsKey(tableName))
                    {
                        var diff = result.TableDifferences[tableName];
                        if (diff.ColumnsOnlyInDatabase.Any())
                        {
                            sb.AppendLine("   • Columnas solo en BD:");
                            foreach (var col in diff.ColumnsOnlyInDatabase)
                            {
                                sb.AppendLine($"     - {col}");
                            }
                        }

                        if (diff.ColumnsOnlyInFiles.Any())
                        {
                            sb.AppendLine("   • Columnas solo en archivo:");
                            foreach (var col in diff.ColumnsOnlyInFiles)
                            {
                                sb.AppendLine($"     - {col}");
                            }
                        }
                    }
                }
                sb.AppendLine();
            }

            var totalTables = allTables.Count();
            var matchingTables = allTables.Count(t => IsTablePerfectMatch(t, result));
            sb.AppendLine($"\nResumen:");
            sb.AppendLine($"Total de tablas revisadas: {totalTables}");
            sb.AppendLine($"Tablas congruentes: {matchingTables}");
            sb.AppendLine($"Tablas con diferencias: {totalTables - matchingTables}");

            return sb.ToString();
        }
    }
}
