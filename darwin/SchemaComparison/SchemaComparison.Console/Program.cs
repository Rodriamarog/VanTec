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
            
            // Run SchemaComparisonTool
            var comparisonTool = new SchemaComparisonTool(connectionString, entityFilesPath);
            var result = comparisonTool.CompareSchemas();
            
            string report = comparisonTool.GenerateReport(result);
            Console.WriteLine(report);

            // Guardar reporte en la carpeta Reportes
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = Path.Combine(reportsFolderPath, $"ReporteComparacion_{timestamp}.txt");
            File.WriteAllText(fileName, report);
            
            Console.WriteLine($"\nReporte guardado en: {fileName}");
            
            // Mostrar un mensaje más amigable de la ubicación del reporte
            string relativePath = Path.Combine("SchemaComparison.Console", "Reportes",
                                             $"ReporteComparacion_{timestamp}.txt");
            Console.WriteLine($"Reporte guardado en la carpeta del proyecto: {relativePath}");
            
            // Run DetailedSchemaAnalyzer
            var analyzer = new DetailedSchemaAnalyzer(entityFilesPath, connectionString);
            string analyzerReport = await analyzer.GenerateDetailedReportAsync();
            Console.WriteLine(analyzerReport);
            
            // Guardar reporte detallado en la carpeta Reportes
            string analyzerFileName = Path.Combine(reportsFolderPath, $"ReporteDarwinContext_{timestamp}.txt");
            File.WriteAllText(analyzerFileName, analyzerReport);
            
            Console.WriteLine($"\nReporte detallado guardado en: {analyzerFileName}");
            
            // Mostrar un mensaje más amigable de la ubicación del reporte detallado
            string analyzerRelativePath = Path.Combine("SchemaComparison.Console", "Reportes",
                                             $"ReporteDarwinContext_{timestamp}.txt");
            Console.WriteLine($"Reporte detallado guardado en la carpeta del proyecto: {analyzerRelativePath}");

            // Generar reporte PDF combinado
            Console.WriteLine("\nGenerando reporte PDF combinado...");
            string pdfFileName = Path.Combine(reportsFolderPath, $"ReporteCompleto_{timestamp}.pdf");
            var htmlGenerator = new HTMLReportGenerator(report, analyzerReport);
            await htmlGenerator.GeneratePDFAsync(pdfFileName);

            Console.WriteLine($"Reporte PDF combinado generado en: {pdfFileName}");
            string pdfRelativePath = Path.Combine("SchemaComparison.Console", "Reportes",
                                             $"ReporteCompleto_{timestamp}.pdf");
            Console.WriteLine($"Reporte PDF guardado en la carpeta del proyecto: {pdfRelativePath}");
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
