using System.Text.RegularExpressions;

namespace CaseConverterBlazor.Data;

public class CaseConverterService
{
    private Dictionary<string, string> _fileMapping;
    private Dictionary<string, string> _classNameMapping;

    public CaseConverterService()
    {
        _fileMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _classNameMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<List<string>> AnalyzeChanges(string sqlServerPath, string mariaDbPath)
    {
        var results = new List<string>();
        try
        {
            BuildFileMappings(sqlServerPath, mariaDbPath, results);
            BuildClassNameMappings(sqlServerPath);

            foreach (var mapping in _fileMapping)
            {
                var mariaFile = mapping.Key;
                var sqlFile = mapping.Value;
                
                var mariaContent = await File.ReadAllTextAsync(mariaFile);
                var sqlContent = await File.ReadAllTextAsync(sqlFile);

                results.Add($"Will rename: {Path.GetFileName(mariaFile)} → {Path.GetFileName(sqlFile)}");

                // Preview content changes
                var modifiedContent = UpdateFileContent(mariaContent);
                if (modifiedContent != mariaContent)
                {
                    results.Add("  Content will be updated to match SQL Server casing");
                }
            }
        }
        catch (Exception ex)
        {
            results.Add($"Error: {ex.Message}");
        }

        return results;
    }

    public async Task<List<string>> ApplyChanges(string sqlServerPath, string mariaDbPath)
    {
        var results = new List<string>();
        try
        {
            BuildFileMappings(sqlServerPath, mariaDbPath, results);
            BuildClassNameMappings(sqlServerPath);

            // Create backup directory
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var backupDir = Path.Combine(mariaDbPath, "Backups", $"Backup_{timestamp}");
            Directory.CreateDirectory(backupDir);

            foreach (var mapping in _fileMapping)
            {
                var mariaFile = mapping.Key;
                var sqlFile = mapping.Value;

                // Backup original file
                var backupPath = Path.Combine(backupDir, Path.GetFileName(mariaFile));
                File.Copy(mariaFile, backupPath, true);

                // Update content
                var content = await File.ReadAllTextAsync(mariaFile);
                var modifiedContent = UpdateFileContent(content);

                // Save with temporary name
                var tempFile = mariaFile + ".temp";
                await File.WriteAllTextAsync(tempFile, modifiedContent);

                // Delete original and rename temp file
                File.Delete(mariaFile);
                File.Move(tempFile, Path.Combine(
                    Path.GetDirectoryName(mariaFile)!,
                    Path.GetFileName(sqlFile)
                ));

                results.Add($"Updated: {Path.GetFileName(mariaFile)} → {Path.GetFileName(sqlFile)}");
            }

            results.Add($"\nBackup created at: {backupDir}");
        }
        catch (Exception ex)
        {
            results.Add($"Error: {ex.Message}");
        }

        return results;
    }

    private void BuildFileMappings(string sqlServerPath, string mariaDbPath, List<string> results)
    {
        _fileMapping.Clear();
        
        var sqlServerFiles = Directory.GetFiles(sqlServerPath, "VT_*.cs", SearchOption.AllDirectories);
        var mariaDbFiles = Directory.GetFiles(mariaDbPath, "vt_*.cs", SearchOption.AllDirectories);

        results.Add($"Found {sqlServerFiles.Length} files in SQL Server directory");
        results.Add($"Found {mariaDbFiles.Length} files in MariaDB directory");

        foreach (var sqlFile in sqlServerFiles)
        {
            var sqlFileName = Path.GetFileName(sqlFile);
            var matchingMariaFile = mariaDbFiles.FirstOrDefault(f => 
                Path.GetFileName(f).Equals(sqlFileName, StringComparison.OrdinalIgnoreCase));

            if (matchingMariaFile != null)
            {
                _fileMapping[matchingMariaFile] = sqlFile;
            }
        }

        results.Add($"Matched {_fileMapping.Count} files for processing");
    }

    private void BuildClassNameMappings(string sqlServerPath)
    {
        _classNameMapping.Clear();
        
        var sqlServerFiles = Directory.GetFiles(sqlServerPath, "VT_*.cs", SearchOption.AllDirectories);
        
        foreach (var sqlFile in sqlServerFiles)
        {
            var className = Path.GetFileNameWithoutExtension(sqlFile);
            _classNameMapping[className.ToLower()] = className;
            _classNameMapping[className.Replace("VT_", "vt_")] = className;
        }
    }

    private string UpdateFileContent(string content)
    {
        var result = content;

        foreach (var mapping in _classNameMapping)
        {
            // Update class declarations
            result = Regex.Replace(
                result,
                $@"class\s+{mapping.Key}\b",
                $"class {mapping.Value}",
                RegexOptions.Multiline
            );

            // Update virtual properties
            result = Regex.Replace(
                result,
                $@"\bvirtual\s+{mapping.Key}\b",
                $"virtual {mapping.Value}",
                RegexOptions.Multiline
            );

            // Update other references
            result = Regex.Replace(
                result,
                $@"\b{mapping.Key}\b",
                mapping.Value,
                RegexOptions.Multiline
            );
        }

        return result;
    }
}