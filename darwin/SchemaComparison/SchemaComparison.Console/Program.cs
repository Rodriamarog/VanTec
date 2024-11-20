using System;
using System.IO;
using System.Reflection;
using SchemaComparison.Core;

class Program
{
    static void Main(string[] args)
    {
        try
        {
            string connectionString = "Server=DESKTOP-AUSLRP2;Database=Darwin;Trusted_Connection=True;";
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