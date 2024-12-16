using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using System.IO;

namespace SchemaComparison.Core
{
    public class IndexComparisonTool
    {
        private readonly string _connectionString;
        private readonly string _entityFilesPath;

        public IndexComparisonTool(string connectionString, string entityFilesPath)
        {
            _connectionString = connectionString;
            _entityFilesPath = entityFilesPath;
        }

        public class IndexInfo
        {
            public required string Name { get; set; }
            public required string TableName { get; set; }
            public bool IsUnique { get; set; }
            public List<string> Columns { get; set; } = new();
            public string Source { get; set; } = ""; // "Database" or "Code"
            public string DefinitionInCode { get; set; } = ""; // Store the original code definition for comparison
        }

        private async Task<List<IndexInfo>> GetDatabaseIndexesAsync()
        {
            var indexes = new List<IndexInfo>();
            
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = @"
                SELECT 
                    REPLACE(t.name, 'VT_', '') AS TableName,
                    i.name AS IndexName,
                    i.is_unique AS IsUnique,
                    c.name AS ColumnName,
                    ic.key_ordinal AS KeyOrdinal
                FROM sys.tables t
                INNER JOIN sys.indexes i ON t.object_id = i.object_id
                INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
                WHERE t.name LIKE 'VT[_]%'
                    AND i.name IS NOT NULL
                    AND i.is_primary_key = 0  -- Exclude primary keys
                    AND i.type_desc = 'NONCLUSTERED' -- Only get non-clustered indexes
                ORDER BY t.name, i.name, ic.key_ordinal";

            using var command = new SqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();

            var currentIndex = new IndexInfo { Name = "", TableName = "" };

            while (await reader.ReadAsync())
            {
                var tableName = reader.GetString(reader.GetOrdinal("TableName"));
                var indexName = reader.GetString(reader.GetOrdinal("IndexName"));
                var columnName = reader.GetString(reader.GetOrdinal("ColumnName"));
                var isUnique = reader.GetBoolean(reader.GetOrdinal("IsUnique"));

                if (currentIndex.Name != indexName || currentIndex.TableName != tableName)
                {
                    if (!string.IsNullOrEmpty(currentIndex.Name))
                    {
                        indexes.Add(currentIndex);
                    }

                    currentIndex = new IndexInfo
                    {
                        Name = indexName,
                        TableName = tableName,
                        IsUnique = isUnique,
                        Source = "Database"
                    };
                }

                currentIndex.Columns.Add(columnName);
            }

            if (!string.IsNullOrEmpty(currentIndex.Name))
            {
                indexes.Add(currentIndex);
            }

            return indexes;
        }

        private List<IndexInfo> GetFluentAPIIndexes()
        {
            var indexes = new List<IndexInfo>();
            var contextFile = Directory.GetFiles(_entityFilesPath, "DarwinContext.cs", SearchOption.AllDirectories).FirstOrDefault();
            
            if (string.IsNullOrEmpty(contextFile))
            {
                Console.WriteLine("⚠️ No se encontró el archivo DarwinContext.cs");
                return indexes;
            }

            var content = File.ReadAllText(contextFile);
            
            // Find all entity configurations
            var entityPattern = @"modelBuilder\.Entity<VT_(\w+)>\(entity\s*=>\s*{(.*?)}\s*\);";
            var entityMatches = Regex.Matches(content, entityPattern, RegexOptions.Singleline);

            foreach (Match entityMatch in entityMatches)
            {
                var tableName = entityMatch.Groups[1].Value;
                var configBlock = entityMatch.Groups[2].Value;

                // Find all HasIndex lines
                var indexLines = configBlock.Split('\n')
                    .Where(line => line.Trim().StartsWith("entity.HasIndex"))
                    .Select(line => line.Trim())
                    .ToList();

                foreach (var indexLine in indexLines)
                {
                    // Debug output
                    Console.WriteLine($"\nProcessing index line for {tableName}:");
                    Console.WriteLine(indexLine);

                    try
                    {
                        // Extract index name
                        var indexNameMatch = Regex.Match(indexLine, @"""([^""]+)""");
                        if (!indexNameMatch.Success) continue;
                        
                        var indexName = indexNameMatch.Groups[1].Value;
                        Console.WriteLine($"Found index name: {indexName}");

                        var index = new IndexInfo
                        {
                            Name = indexName,
                            TableName = tableName,
                            Source = "Code",
                            DefinitionInCode = indexLine
                        };

                        // Check if it's a composite index
                        if (indexLine.Contains("new {"))
                        {
                            var columnsMatch = Regex.Match(indexLine, @"new\s*{\s*([^}]+)}");
                            if (columnsMatch.Success)
                            {
                                var columnsText = columnsMatch.Groups[1].Value;
                                var columns = Regex.Matches(columnsText, @"e\.(\w+)")
                                    .Cast<Match>()
                                    .Select(m => m.Groups[1].Value)
                                    .ToList();
                                index.Columns.AddRange(columns);
                                Console.WriteLine($"Found composite columns: {string.Join(", ", columns)}");
                            }
                        }
                        else
                        {
                            var singleColumnMatch = Regex.Match(indexLine, @"e\s*=>\s*e\.(\w+)");
                            if (singleColumnMatch.Success)
                            {
                                var columnName = singleColumnMatch.Groups[1].Value;
                                index.Columns.Add(columnName);
                                Console.WriteLine($"Found single column: {columnName}");
                            }
                        }

                        index.IsUnique = indexLine.Contains(".IsUnique()");
                        Console.WriteLine($"IsUnique: {index.IsUnique}");

                        if (index.Columns.Any())
                        {
                            indexes.Add(index);
                            Console.WriteLine("Index added successfully");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing index line: {ex.Message}");
                    }
                }
            }

            return indexes;
        }

        public async Task<string> GenerateReportAsync()
        {
            var dbIndexes = await GetDatabaseIndexesAsync();
            var codeIndexes = GetFluentAPIIndexes();
            var sb = new StringBuilder();

            sb.AppendLine("=== REPORTE DE COMPARACIÓN DE ÍNDICES ===\n");
            
            // Count total indexes
            sb.AppendLine($"Total de índices en la base de datos: {dbIndexes.Count}");
            sb.AppendLine($"Total de índices en el código: {codeIndexes.Count}\n");

            // 1. Indexes that exist only in database
            var onlyInDb = dbIndexes
                .Where(db => !codeIndexes.Any(code => 
                    code.Name == db.Name && 
                    code.TableName == db.TableName))
                .OrderBy(i => i.TableName)
                .ThenBy(i => i.Name)
                .ToList();

            sb.AppendLine("1. ÍNDICES QUE EXISTEN EN BD PERO NO EN CÓDIGO:");
            foreach (var index in onlyInDb)
            {
                sb.AppendLine($"\nTabla: VT_{index.TableName}");
                sb.AppendLine($"Índice: {index.Name}");
                sb.AppendLine($"Único: {(index.IsUnique ? "Sí" : "No")}");
                sb.AppendLine("Columnas:");
                foreach (var col in index.Columns)
                {
                    sb.AppendLine($"  - {col}");
                }
            }

            // 2. Indexes that exist only in code
            var onlyInCode = codeIndexes
                .Where(code => !dbIndexes.Any(db => 
                    db.Name == code.Name && 
                    db.TableName == code.TableName))
                .OrderBy(i => i.TableName)
                .ThenBy(i => i.Name)
                .ToList();

            sb.AppendLine("\n2. ÍNDICES QUE EXISTEN EN CÓDIGO PERO NO EN BD:");
            foreach (var index in onlyInCode)
            {
                sb.AppendLine($"\nTabla: VT_{index.TableName}");
                sb.AppendLine($"Índice: {index.Name}");
                sb.AppendLine($"Único: {(index.IsUnique ? "Sí" : "No")}");
                sb.AppendLine("Columnas:");
                foreach (var col in index.Columns)
                {
                    sb.AppendLine($"  - {col}");
                }
                sb.AppendLine("Definición en código:");
                sb.AppendLine($"  {index.DefinitionInCode}");
            }

            // 3. Indexes with differences
            sb.AppendLine("\n3. ÍNDICES CON DIFERENCIAS EN LA CONFIGURACIÓN:");
            foreach (var dbIndex in dbIndexes)
            {
                var codeIndex = codeIndexes.FirstOrDefault(c => 
                    c.Name == dbIndex.Name && 
                    c.TableName == dbIndex.TableName);

                if (codeIndex != null)
                {
                    var differences = new List<string>();

                    if (dbIndex.IsUnique != codeIndex.IsUnique)
                    {
                        differences.Add($"Propiedad Unique: BD={dbIndex.IsUnique}, Código={codeIndex.IsUnique}");
                    }

                    if (!dbIndex.Columns.SequenceEqual(codeIndex.Columns))
                    {
                        differences.Add($"Columnas en BD: {string.Join(", ", dbIndex.Columns)}");
                        differences.Add($"Columnas en Código: {string.Join(", ", codeIndex.Columns)}");
                    }

                    if (differences.Any())
                    {
                        sb.AppendLine($"\nTabla: VT_{dbIndex.TableName}");
                        sb.AppendLine($"Índice: {dbIndex.Name}");
                        sb.AppendLine("Diferencias encontradas:");
                        foreach (var diff in differences)
                        {
                            sb.AppendLine($"  - {diff}");
                        }
                        sb.AppendLine("Definición en código:");
                        sb.AppendLine($"  {codeIndex.DefinitionInCode}");
                    }
                }
            }

            // Summary
            sb.AppendLine("\nRESUMEN:");
            sb.AppendLine($"Total de índices solo en BD: {onlyInDb.Count}");
            sb.AppendLine($"Total de índices solo en código: {onlyInCode.Count}");
            var indexesWithDiffs = dbIndexes.Count(db => 
                codeIndexes.Any(c => c.Name == db.Name && 
                                   c.TableName == db.TableName && 
                                   (!c.Columns.SequenceEqual(db.Columns) || 
                                    c.IsUnique != db.IsUnique)));
            sb.AppendLine($"Total de índices con diferencias: {indexesWithDiffs}");

            return sb.ToString();
        }
    }
}