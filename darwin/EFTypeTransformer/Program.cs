using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;

class Program
{
    static void Main(string[] args)
    {
        // Hardcoded paths - modify these to match your directory structure
        var mariaDbPath = @"C:\netC#\apps\Datos_MariaDB\Datos_MariaDB\Datos\Datos\Diccionario";
        var sqlServerPath = @"C:\netC#\apps\Datos_SQLServer\Datos_SQLServer\Datos\Diccionario";
        
        // First do a dry run
        Console.WriteLine("=== PERFORMING DRY RUN ===");
        var transformer = new EntityTypeTransformer(mariaDbPath, sqlServerPath);
        transformer.ProcessFiles(isDryRun: true);

        Console.WriteLine("\nWould you like to proceed with the actual changes? (y/n)");
        var response = Console.ReadLine()?.ToLower();
        
        if (response == "y")
        {
            Console.WriteLine("\n=== PERFORMING ACTUAL CHANGES ===");
            transformer.ProcessFiles(isDryRun: false);
        }
    }
}

public class EntityTypeTransformer
{
    private readonly string _mariaDbPath;
    private readonly string _sqlServerPath;
    private readonly Dictionary<string, string> _fileMapping;
    private readonly List<string> _dryRunResults;

    public EntityTypeTransformer(string mariaDbPath, string sqlServerPath)
    {
        _mariaDbPath = mariaDbPath;
        _sqlServerPath = sqlServerPath;
        _fileMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _dryRunResults = new List<string>();
    }

    public void ProcessFiles(bool isDryRun)
    {
        BuildFileMapping();

        foreach (var mapping in _fileMapping)
        {
            ProcessFilePair(mapping.Key, mapping.Value, isDryRun);
        }

        if (isDryRun && _dryRunResults.Any())
        {
            Console.WriteLine("\n=== DRY RUN RESULTS ===");
            Console.WriteLine("The following changes would be made:\n");
            foreach (var result in _dryRunResults)
            {
                Console.WriteLine(result);
            }
            
            // Save results to a file for review
            var resultsPath = Path.Combine(Directory.GetCurrentDirectory(), "dry_run_results.txt");
            File.WriteAllLines(resultsPath, _dryRunResults);
            Console.WriteLine($"\nDetailed results have been saved to: {resultsPath}");
        }
    }

    private void BuildFileMapping()
    {
        var mariaDbFiles = Directory.GetFiles(_mariaDbPath, "VT_*.cs", SearchOption.AllDirectories);
        var sqlServerFiles = Directory.GetFiles(_sqlServerPath, "VT_*.cs", SearchOption.AllDirectories);

        foreach (var mariaDbFile in mariaDbFiles)
        {
            var fileName = Path.GetFileName(mariaDbFile);
            var matchingSqlFile = sqlServerFiles.FirstOrDefault(f => 
                Path.GetFileName(f).Equals(fileName, StringComparison.OrdinalIgnoreCase));

            if (matchingSqlFile != null)
            {
                _fileMapping[mariaDbFile] = matchingSqlFile;
                Console.WriteLine($"Matched: {fileName}");
            }
            else
            {
                Console.WriteLine($"No match found for: {fileName}");
            }
        }

        Console.WriteLine($"\nSummary:");
        Console.WriteLine($"MariaDB VT_*.cs files found: {mariaDbFiles.Length}");
        Console.WriteLine($"SQLServer VT_*.cs files found: {sqlServerFiles.Length}");
        Console.WriteLine($"Matched pairs found: {_fileMapping.Count}\n");
    }

    private void ProcessFilePair(string mariaDbFile, string sqlServerFile, bool isDryRun)
    {
        var mariaDbContent = File.ReadAllText(mariaDbFile);
        var sqlServerContent = File.ReadAllText(sqlServerFile);

        var mariaDbProperties = ParseProperties(mariaDbContent);
        var sqlServerProperties = ParseProperties(sqlServerContent);

        var modifications = new List<(string PropertyName, string Original, string Modified)>();

        foreach (var prop in mariaDbProperties)
        {
            if (prop.Type.Contains("TimeOnly?"))
            {
                var matchingSqlProp = sqlServerProperties.FirstOrDefault(p => 
                    p.Name == prop.Name && p.Type.Contains("DateTime?"));

                if (matchingSqlProp != null)
                {
                    var originalDeclaration = matchingSqlProp.FullDeclaration;
                    var modifiedDeclaration = CreateConditionalDeclaration(prop.Name);
                    modifications.Add((prop.Name, originalDeclaration, modifiedDeclaration));
                }
            }
        }

        if (modifications.Any())
        {
            var fileName = Path.GetFileName(sqlServerFile);
            
            if (isDryRun)
            {
                _dryRunResults.Add($"\nFile: {fileName}");
                _dryRunResults.Add("Changes to be made:");
                
                foreach (var mod in modifications)
                {
                    _dryRunResults.Add($"\nProperty: {mod.PropertyName}");
                    _dryRunResults.Add("Original:");
                    _dryRunResults.Add(mod.Original);
                    _dryRunResults.Add("Will be changed to:");
                    _dryRunResults.Add(mod.Modified);
                    _dryRunResults.Add(new string('-', 50));
                }
            }
            else
            {
                Console.WriteLine($"\nProcessing: {fileName}");
                var modifiedContent = sqlServerContent;
                
                foreach (var mod in modifications)
                {
                    modifiedContent = modifiedContent.Replace(mod.Original, mod.Modified);
                    Console.WriteLine($"  Modified property: {mod.PropertyName}");
                }

                var backupPath = sqlServerFile + ".bak";
                File.Copy(sqlServerFile, backupPath, true);
                File.WriteAllText(sqlServerFile, modifiedContent);

                Console.WriteLine($"  Total modifications: {modifications.Count}");
                Console.WriteLine($"  Backup created: {Path.GetFileName(backupPath)}");
            }
        }
    }

    private class PropertyInfo
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string FullDeclaration { get; set; }
    }

    private List<PropertyInfo> ParseProperties(string content)
    {
        var properties = new List<PropertyInfo>();
        var propertyPattern = @"public\s+([^\s]+)\s+([^\s]+)\s*{\s*get;\s*set;\s*}";
        var matches = Regex.Matches(content, propertyPattern);

        foreach (Match match in matches)
        {
            properties.Add(new PropertyInfo
            {
                Type = match.Groups[1].Value,
                Name = match.Groups[2].Value,
                FullDeclaration = match.Value
            });
        }

        return properties;
    }

    private string CreateConditionalDeclaration(string propertyName)
    {
        return $@"#if CompilandoBackendSQLServer
    public DateTime? {propertyName} {{ get; set; }}
#else
    public TimeOnly? {propertyName} {{ get; set; }}
#endif";
    }
}