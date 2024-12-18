using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;

class Program
{
    static void Main(string[] args)
    {
        // Modify these paths to match your directory structure
        var mariaDbPath = @"C:\netC#\apps\Datos_MariaDB\Datos_MariaDB\Datos\Datos\Diccionario";
        var sqlServerPath = @"C:\netC#\apps\Datos_SQLServer\Datos_SQLServer\Datos\Diccionario";
        
        var converter = new CaseConverter(mariaDbPath, sqlServerPath);
        
        // First do a dry run
        Console.WriteLine("=== PERFORMING DRY RUN ===");
        converter.ProcessFiles(isDryRun: true);

        Console.WriteLine("\nWould you like to proceed with the actual changes? (y/n)");
        var response = Console.ReadLine()?.ToLower();
        
        if (response == "y")
        {
            Console.WriteLine("\n=== PERFORMING ACTUAL CHANGES ===");
            converter.ProcessFiles(isDryRun: false);
        }
    }
}

public class CaseConverter
{
    private readonly string _mariaDbPath;
    private readonly string _sqlServerPath;
    private readonly Dictionary<string, string> _fileMapping;
    private readonly Dictionary<string, string> _classNameMapping;
    private readonly List<string> _dryRunResults;
    private readonly string _backupFolderPath;

    public CaseConverter(string mariaDbPath, string sqlServerPath)
    {
        _mariaDbPath = mariaDbPath;
        _sqlServerPath = sqlServerPath;
        _fileMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _classNameMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _dryRunResults = new List<string>();
        
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var backupDir = Path.GetDirectoryName(mariaDbPath);
        if (backupDir != null)
        {
            _backupFolderPath = Path.Combine(backupDir, "Backups", $"Backup_{timestamp}");
            Directory.CreateDirectory(_backupFolderPath);
        }
        else
        {
            throw new DirectoryNotFoundException("MariaDB directory path is invalid");
        }
    }

    public void ProcessFiles(bool isDryRun)
    {
        // Build mappings
        BuildFileMappings();
        BuildClassNameMappings();

        if (isDryRun)
        {
            PerformDryRun();
        }
        else
        {
            PerformActualChanges();
        }
    }

    private void BuildFileMappings()
    {
        var sqlServerFiles = Directory.GetFiles(_sqlServerPath, "VT_*.cs", SearchOption.AllDirectories);
        var mariaDbFiles = Directory.GetFiles(_mariaDbPath, "vt_*.cs", SearchOption.AllDirectories)
            .Where(f => !IsAlreadyCamelCase(f)); // Skip files already in CamelCase

        foreach (var sqlFile in sqlServerFiles)
        {
            var sqlFileName = Path.GetFileName(sqlFile);
            var matchingMariaFile = mariaDbFiles.FirstOrDefault(f => 
                Path.GetFileName(f).Equals(sqlFileName, StringComparison.OrdinalIgnoreCase));

            if (matchingMariaFile != null)
            {
                _fileMapping[matchingMariaFile] = sqlFile;
                Console.WriteLine($"Matched: {Path.GetFileName(matchingMariaFile)} -> {sqlFileName}");
            }
        }

        Console.WriteLine($"\nFound {_fileMapping.Count} files to convert");
    }

    private bool IsAlreadyCamelCase(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        return fileName.StartsWith("VT_", StringComparison.Ordinal);
    }

    private void BuildClassNameMappings()
    {
        foreach (var mapping in _fileMapping)
        {
            var mariaContent = File.ReadAllText(mapping.Key);
            var sqlContent = File.ReadAllText(mapping.Value);

            // Extract class names
            var mariaClassName = ExtractClassName(mariaContent);
            var sqlClassName = ExtractClassName(sqlContent);

            if (mariaClassName != null && sqlClassName != null)
            {
                _classNameMapping[mariaClassName] = sqlClassName;
            }
        }
    }

    private string? ExtractClassName(string content)
    {
        var match = Regex.Match(content, @"public\s+partial\s+class\s+(\w+)");
        return match.Success ? match.Groups[1].Value : null;
    }

    private void PerformDryRun()
    {
        foreach (var mapping in _fileMapping)
        {
            var mariaFile = mapping.Key;
            var sqlFile = mapping.Value;
            
            var mariaContent = File.ReadAllText(mariaFile);
            var sqlContent = File.ReadAllText(sqlFile);

            _dryRunResults.Add($"\nFile: {Path.GetFileName(mariaFile)}");
            _dryRunResults.Add($"Will be renamed to: {Path.GetFileName(sqlFile)}");
            
            var modifiedContent = UpdateFileContent(mariaContent);
            
            _dryRunResults.Add("Content changes:");
            _dryRunResults.Add(CompareContents(mariaContent, modifiedContent));
            _dryRunResults.Add(new string('-', 50));
        }

        // Save dry run results
        var resultsPath = Path.Combine(_backupFolderPath, "dry_run_results.txt");
        File.WriteAllLines(resultsPath, _dryRunResults);
        Console.WriteLine($"\nDetailed results have been saved to: {resultsPath}");
    }
    private void PerformActualChanges()
    {
        foreach (var mapping in _fileMapping)
        {
            var mariaFile = mapping.Key;
            var sqlFile = mapping.Value;
            
            Console.WriteLine($"\nProcessing: {Path.GetFileName(mariaFile)}");

            // Backup original file
            var relativePath = Path.GetRelativePath(_mariaDbPath, mariaFile);
            var backupPath = Path.Combine(_backupFolderPath, relativePath);
            var backupDir = Path.GetDirectoryName(backupPath);
            if (backupDir != null)
            {
                Directory.CreateDirectory(backupDir);
            }
            File.Copy(mariaFile, backupPath, true);

            // Update content
            var content = File.ReadAllText(mariaFile);
            var modifiedContent = UpdateFileContent(content);
            
            // Save with temporary name first
            var tempFileName = Path.GetFileName(mariaFile) + ".temp";
            var tempFilePath = Path.Combine(Path.GetDirectoryName(mariaFile)!, tempFileName);
            
            File.WriteAllText(tempFilePath, modifiedContent);
            
            // Delete original file
            File.Delete(mariaFile);
            
            // Now rename temp file to final name
            var newFileName = Path.GetFileName(sqlFile);
            var finalPath = Path.Combine(Path.GetDirectoryName(mariaFile)!, newFileName);
            File.Move(tempFilePath, finalPath);

            Console.WriteLine($"  Updated and renamed to: {newFileName}");
        }

        // Update DarwinContext.cs
        UpdateDarwinContext();

        Console.WriteLine($"\nAll files have been processed.");
        Console.WriteLine($"Original files backed up to: {_backupFolderPath}");
        Console.WriteLine($"New files are in: {_mariaDbPath}");
    }

    private string UpdateFileContent(string content)
    {
        var result = content;

        // Update class names and references
        foreach (var mapping in _classNameMapping)
        {
            result = Regex.Replace(
                result,
                $@"\b{mapping.Key}\b",
                mapping.Value,
                RegexOptions.Multiline
            );
        }

        return result;
    }

    private void UpdateDarwinContext()
    {
        var darwinContextPath = Path.Combine(_mariaDbPath, "DarwinContext.cs");
        if (File.Exists(darwinContextPath))
        {
            Console.WriteLine("\nUpdating DarwinContext.cs...");
            
            // Backup
            var backupPath = Path.Combine(_backupFolderPath, "DarwinContext.cs");
            File.Copy(darwinContextPath, backupPath, true);

            // Update
            var content = File.ReadAllText(darwinContextPath);
            var modifiedContent = UpdateFileContent(content);
            File.WriteAllText(darwinContextPath, modifiedContent);
            
            Console.WriteLine("DarwinContext.cs has been updated");
        }
    }

    private string CompareContents(string original, string modified)
    {
        var differences = new List<string>();
        
        var originalLines = original.Split('\n');
        var modifiedLines = modified.Split('\n');
        
        for (int i = 0; i < originalLines.Length; i++)
        {
            if (i < modifiedLines.Length && originalLines[i] != modifiedLines[i])
            {
                differences.Add($"Original: {originalLines[i].Trim()}");
                differences.Add($"Modified: {modifiedLines[i].Trim()}");
                differences.Add("");
            }
        }
        
        return string.Join('\n', differences);
    }
}