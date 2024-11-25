using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace SchemaComparison.Core
{
    public class HTMLReportGenerator
    {
        private readonly string _basicReport;
        private readonly string _detailedReport;

        public HTMLReportGenerator(string basicReport, string detailedReport)
        {
            _basicReport = basicReport;
            _detailedReport = detailedReport;
        }

        private string GenerateHTML()
        {
            var html = new StringBuilder();
            html.AppendLine(@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {
            font-family: Arial, sans-serif;
            line-height: 1.6;
            margin: 40px;
            color: #333;
        }
        .header {
            background-color: #2c3e50;
            color: white;
            padding: 20px;
            margin-bottom: 30px;
            border-radius: 5px;
        }
        .section {
            margin-bottom: 30px;
            padding: 20px;
            background-color: #f9f9f9;
            border-radius: 5px;
            box-shadow: 0 2px 5px rgba(0,0,0,0.1);
        }
        .section-title {
            color: #2c3e50;
            border-bottom: 2px solid #2c3e50;
            padding-bottom: 10px;
            margin-bottom: 20px;
        }
        .check {
            color: #27ae60;
        }
        .cross {
            color: #c0392b;
        }
        .warning {
            color: #f39c12;
        }
        pre {
            white-space: pre-wrap;
            background-color: #f8f9fa;
            padding: 15px;
            border-radius: 5px;
            border: 1px solid #dee2e6;
            font-size: 14px;
            line-height: 1.4;
            overflow-x: auto;
        }
        table {
            width: 100%;
            border-collapse: collapse;
            margin: 15px 0;
        }
        th, td {
            padding: 12px;
            border: 1px solid #dee2e6;
            text-align: left;
        }
        th {
            background-color: #2c3e50;
            color: white;
        }
        tr:nth-child(even) {
            background-color: #f8f9fa;
        }
        @media print {
            body {
                margin: 0;
                padding: 20px;
            }
            pre {
                white-space: pre-wrap;
                word-wrap: break-word;
            }
        }
    </style>
</head>
<body>
    <div class='header'>
        <h1>Reporte de Análisis de Esquema de Base de Datos</h1>
        <p>Generado el: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss") + @"</p>
    </div>

    <div class='section'>
        <h2 class='section-title'>Reporte General</h2>
        <pre>" + _basicReport.Replace("✅", "<span class='check'>✅</span>")
                           .Replace("❌", "<span class='cross'>❌</span>")
                           .Replace("⚠️", "<span class='warning'>⚠️</span>") + @"</pre>
    </div>

    <div class='section'>
        <h2 class='section-title'>Reporte Detallado</h2>
        <pre>" + _detailedReport.Replace("✅", "<span class='check'>✅</span>")
                               .Replace("❌", "<span class='cross'>❌</span>")
                               .Replace("⚠️", "<span class='warning'>⚠️</span>") + @"</pre>
    </div>
</body>
</html>");

            return html.ToString();
        }

        public async Task GeneratePDFAsync(string outputPath)
        {
            Console.WriteLine("Iniciando generación de PDF...");
            
            Console.WriteLine("Verificando instalación de Chromium...");
            await new BrowserFetcher().DownloadAsync();

            Console.WriteLine("Iniciando navegador...");
            var browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true
            });

            try
            {
                Console.WriteLine("Generando contenido...");
                using var page = await browser.NewPageAsync();
                await page.SetContentAsync(GenerateHTML());

                Console.WriteLine("Creando PDF...");
                await page.PdfAsync(outputPath, new PdfOptions
                {
                    Format = PaperFormat.A4,
                    PrintBackground = true,
                    MarginOptions = new MarginOptions
                    {
                        Top = "20mm",
                        Bottom = "20mm",
                        Left = "20mm",
                        Right = "20mm"
                    }
                });

                Console.WriteLine("PDF generado exitosamente.");
            }
            finally
            {
                await browser.CloseAsync();
            }
        }
    }
}
