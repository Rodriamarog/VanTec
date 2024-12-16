using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using System.Text;
using System.Threading.Tasks;

namespace SchemaComparison.Core
{
    public class DetailedSchemaAnalyzer
    {
        private readonly string _entityFilesPath;
        private readonly string _connectionString;

        public DetailedSchemaAnalyzer(string entityFilesPath, string connectionString)
        {
            _entityFilesPath = entityFilesPath;
            _connectionString = connectionString;
        }

        public class ColumnDetails
        {
            public required string Name { get; set; }
            public required string DataType { get; set; }
            public bool IsNullable { get; set; }
            public int? MaxLength { get; set; }
            public string? Precision { get; set; }
            public string? Scale { get; set; }
        }

        public class TableDetails
        {
            public required string Name { get; set; }
            public Dictionary<string, ColumnDetails> Columns { get; set; } = new();
        }

        private async Task<Dictionary<string, TableDetails>> GetDatabaseSchemaAsync()
        {
            var schema = new Dictionary<string, TableDetails>();

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = @"
                SELECT 
                    REPLACE(t.name, 'VT_', '') AS TableName,
                    c.name AS ColumnName,
                    tp.name AS DataType,
                    c.max_length AS MaxLength,
                    c.is_nullable AS IsNullable,
                    c.precision AS Precision,
                    c.scale AS Scale
                FROM sys.tables t
                INNER JOIN sys.columns c ON t.object_id = c.object_id
                INNER JOIN sys.types tp ON c.user_type_id = tp.user_type_id
                WHERE t.name LIKE 'VT[_]%'
                ORDER BY t.name, c.column_id";

            using var command = new SqlCommand(query, connection) { CommandTimeout = 300 };
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var tableName = reader.GetString(reader.GetOrdinal("TableName"));
                var columnName = reader.GetString(reader.GetOrdinal("ColumnName"));
                var dataType = reader.GetString(reader.GetOrdinal("DataType"));

                if (!schema.TryGetValue(tableName, out var tableDetails))
                {
                    tableDetails = new TableDetails { Name = tableName };
                    schema[tableName] = tableDetails;
                }

                tableDetails.Columns[columnName] = new ColumnDetails
                {
                    Name = columnName,
                    DataType = dataType,
                    IsNullable = reader.GetBoolean(reader.GetOrdinal("IsNullable")),
                    MaxLength = !reader.IsDBNull(reader.GetOrdinal("MaxLength")) 
                        ? reader.GetInt16(reader.GetOrdinal("MaxLength")) 
                        : null,
                    Precision = !reader.IsDBNull(reader.GetOrdinal("Precision")) 
                        ? reader.GetByte(reader.GetOrdinal("Precision")).ToString() 
                        : null,
                    Scale = !reader.IsDBNull(reader.GetOrdinal("Scale")) 
                        ? reader.GetByte(reader.GetOrdinal("Scale")).ToString() 
                        : null
                };
            }

            return schema;
        }

        private Dictionary<string, TableDetails> GetEntitySchema()
        {
            var schema = new Dictionary<string, TableDetails>();
            var files = Directory.GetFiles(_entityFilesPath, "VT_*.cs");

            foreach (var file in files)
            {
                var content = File.ReadAllText(file);
                var entity = ParseEntityClass(content);
                if (entity != null)
                {
                    var tableName = entity.Name.Replace("VT_", "");
                    schema[tableName] = entity;
                }
            }

            return schema;
        }

        private TableDetails? ParseEntityClass(string content)
        {
            var classNameMatch = Regex.Match(content, @"public\s+(?:partial\s+)?class\s+(\w+)");
            if (!classNameMatch.Success) return null;

            var entity = new TableDetails
            {
                Name = classNameMatch.Groups[1].Value
            };

            var propertyPattern = @"public\s+([^\s<>]+)(?:<[^>]+>)?\s+(\w+)\s*{\s*get;\s*set;\s*}";
            var matches = Regex.Matches(content, propertyPattern);

            foreach (Match match in matches)
            {
                var propertyType = match.Groups[1].Value;
                var propertyName = match.Groups[2].Value;
                
                if (!propertyType.StartsWith("VT_") && !propertyType.Contains("ICollection"))
                {
                    entity.Columns[propertyName] = new ColumnDetails
                    {
                        Name = propertyName,
                        DataType = MapCSharpToSqlType(propertyType),
                        IsNullable = IsNullableType(propertyType)
                    };
                }
            }

            return entity;
        }

        private string MapCSharpToSqlType(string csharpType)
        {
            return csharpType.ToLower() switch
            {
                "int" => "int",
                "string" => "nvarchar",
                "datetime" => "datetime",
                "bool" => "bit",
                "decimal" => "decimal",
                "double" => "float",
                "float" => "real",
                "guid" => "uniqueidentifier",
                "long" => "bigint",
                "short" => "smallint",
                "byte" => "tinyint",
                _ => csharpType
            };
        }

        private bool IsNullableType(string type)
        {
            return type.EndsWith("?") || type == "string";
        }

        public async Task<string> GenerateDetailedReportAsync()
        {
            var dbSchema = await GetDatabaseSchemaAsync();
            var entitySchema = GetEntitySchema();

            var sb = new StringBuilder();
            sb.AppendLine("=== REPORTE DETALLADO DE DIFERENCIAS ===\n");

            // 1. Tablas que existen solo en la BD
            sb.AppendLine("1. TABLAS QUE EXISTEN EN BD PERO NO EN ARCHIVOS:");
            foreach (var tableName in dbSchema.Keys.Except(entitySchema.Keys).OrderBy(t => t))
            {
                sb.AppendLine($"\nTabla: VT_{tableName}");
                sb.AppendLine("Estructura:");
                foreach (var col in dbSchema[tableName].Columns.Values.OrderBy(c => c.Name))
                {
                    var typeDetails = GetFormattedTypeDetails(col);
                    sb.AppendLine($"  - {col.Name} ({typeDetails})");
                }
            }

            // 2. Tablas que existen solo en archivos
            sb.AppendLine("\n2. TABLAS QUE EXISTEN EN ARCHIVOS PERO NO EN BD:");
            foreach (var tableName in entitySchema.Keys.Except(dbSchema.Keys).OrderBy(t => t))
            {
                sb.AppendLine($"\nTabla: VT_{tableName}");
                sb.AppendLine("Estructura:");
                foreach (var col in entitySchema[tableName].Columns.Values.OrderBy(c => c.Name))
                {
                    var typeDetails = GetFormattedTypeDetails(col);
                    sb.AppendLine($"  - {col.Name} ({typeDetails})");
                }
            }

            // 3. Columnas que existen solo en BD
            sb.AppendLine("\n3. COLUMNAS QUE EXISTEN EN BD PERO NO EN ARCHIVOS:");
            foreach (var tableName in dbSchema.Keys.Intersect(entitySchema.Keys).OrderBy(t => t))
            {
                var dbColumns = dbSchema[tableName].Columns;
                var entityColumns = entitySchema[tableName].Columns;
                var missingColumns = dbColumns.Keys.Except(entityColumns.Keys).ToList();

                if (missingColumns.Any())
                {
                    sb.AppendLine($"\nTabla: VT_{tableName}");
                    foreach (var colName in missingColumns.OrderBy(c => c))
                    {
                        var col = dbColumns[colName];
                        var typeDetails = GetFormattedTypeDetails(col);
                        sb.AppendLine($"  - {col.Name} ({typeDetails})");
                    }
                }
            }

            // 4. Columnas que existen solo en archivos
            sb.AppendLine("\n4. COLUMNAS QUE EXISTEN EN ARCHIVOS PERO NO EN BD:");
            foreach (var tableName in entitySchema.Keys.Intersect(dbSchema.Keys).OrderBy(t => t))
            {
                var dbColumns = dbSchema[tableName].Columns;
                var entityColumns = entitySchema[tableName].Columns;
                var missingColumns = entityColumns.Keys.Except(dbColumns.Keys).ToList();

                if (missingColumns.Any())
                {
                    sb.AppendLine($"\nTabla: VT_{tableName}");
                    foreach (var colName in missingColumns.OrderBy(c => c))
                    {
                        var col = entityColumns[colName];
                        var typeDetails = GetFormattedTypeDetails(col);
                        sb.AppendLine($"  - {col.Name} ({typeDetails})");
                    }
                }
            }

            return sb.ToString();
        }

        private string GetFormattedTypeDetails(ColumnDetails col)
        {
            var details = new StringBuilder(col.DataType);

            if (col.MaxLength.HasValue && col.MaxLength.Value != -1)
            {
                details.Append($"({col.MaxLength.Value})");
            }
            else if (col.MaxLength == -1)
            {
                details.Append("(MAX)");
            }
            else if (!string.IsNullOrEmpty(col.Precision) && !string.IsNullOrEmpty(col.Scale))
            {
                details.Append($"({col.Precision},{col.Scale})");
            }

            details.Append(col.IsNullable ? ", NULL" : ", NOT NULL");

            return details.ToString();
        }
    }
}
