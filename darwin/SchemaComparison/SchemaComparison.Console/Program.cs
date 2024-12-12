using System;
using System.IO;
using System.Reflection;
using SchemaComparison.Core;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            string connectionString = "Server=DESKTOP-AUSLRP2;Database=Darwin;Trusted_Connection=True;TrustServerCertificate=True;";
            string entityFilesPath = @"C:\netC#\apps\Datos_SQLServer\Datos_SQLServer\Datos\Diccionario";
            
            // Obtener la ruta específica del proyecto Console
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string projectPath = Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", ".."));
            
            // Verificar que estamos en la carpeta correcta
            if (!projectPath.EndsWith("SchemaComparison.Console"))
            {
                throw new DirectoryNotFoundException(
                    "No se pudo encontrar el directorio SchemaComparison.Console. " +
                    $"Ruta actual: {projectPath}"
                );
            }
            
            // Crear carpeta Reportes en el directorio específico
            string reportsFolderPath = Path.Combine(projectPath, "Reportes");
            Directory.CreateDirectory(reportsFolderPath);
            
            // Generar timestamp para los archivos
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            // Run SchemaComparisonTool
            Console.WriteLine("\n=== Ejecutando Comparación de Esquemas ===");
            var comparisonTool = new SchemaComparisonTool(connectionString, entityFilesPath);
            var result = comparisonTool.CompareSchemas();
            string report = comparisonTool.GenerateReport(result);
            Console.WriteLine(report);

            // Guardar reporte de esquemas
            string schemaFileName = Path.Combine(reportsFolderPath, $"ReporteComparacion_{timestamp}.txt");
            File.WriteAllText(schemaFileName, report);
            
            // Run DetailedSchemaAnalyzer
            Console.WriteLine("\n=== Ejecutando Análisis Detallado de Esquemas ===");
            var analyzer = new DetailedSchemaAnalyzer(entityFilesPath, connectionString);
            string analyzerReport = await analyzer.GenerateDetailedReportAsync();
            Console.WriteLine(analyzerReport);
            
            // Guardar reporte detallado
            string analyzerFileName = Path.Combine(reportsFolderPath, $"ReporteDarwinContext_{timestamp}.txt");
            File.WriteAllText(analyzerFileName, analyzerReport);

            // Run IndexComparisonTool
            Console.WriteLine("\n=== Ejecutando Comparación de Índices ===");
            var indexTool = new IndexComparisonTool(connectionString, entityFilesPath);
            string indexReport = await indexTool.GenerateReportAsync();
            Console.WriteLine(indexReport);

            // Guardar reporte de índices
            string indexFileName = Path.Combine(reportsFolderPath, $"ReporteIndices_{timestamp}.txt");
            File.WriteAllText(indexFileName, indexReport);

            // Generar reporte PDF combinado con los tres reportes
            Console.WriteLine("\nGenerando reporte PDF combinado...");
            string pdfFileName = Path.Combine(reportsFolderPath, $"ReporteCompleto_{timestamp}.pdf");
            var htmlGenerator = new HTMLReportGenerator(report, analyzerReport, indexReport); // Asumiendo que modificaste HTMLReportGenerator
            await htmlGenerator.GeneratePDFAsync(pdfFileName);

            // Mostrar ubicaciones de los archivos generados
            Console.WriteLine("\nReportes generados:");
            Console.WriteLine($"1. Reporte de Comparación: {Path.GetFileName(schemaFileName)}");
            Console.WriteLine($"2. Reporte Detallado: {Path.GetFileName(analyzerFileName)}");
            Console.WriteLine($"3. Reporte de Índices: {Path.GetFileName(indexFileName)}");
            Console.WriteLine($"4. PDF Combinado: {Path.GetFileName(pdfFileName)}");
            Console.WriteLine($"\nTodos los reportes se encuentran en: {reportsFolderPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine($"StackTrace: {ex.StackTrace}");
        }

        Console.WriteLine("\nPresione cualquier tecla para salir...");
        Console.ReadKey();
    }
}