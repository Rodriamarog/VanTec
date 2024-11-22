using System;
using System.IO;
using System.Text;
using IronPdf;

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
    </style>
</head>
<body>
    <div class='header'>
        <h1>Reporte de Análisis de Esquema</h1>
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

        public void GeneratePDF(string outputPath)
        {
            var html = GenerateHTML();
            var renderer = new ChromePdfRenderer();
            
            // Configurar opciones del PDF
            renderer.RenderingOptions.PaperSize = IronPdf.Rendering.PdfPaperSize.Custom;
            renderer.RenderingOptions.SetCustomPaperSizeInInches(8.5f, 11f);
            renderer.RenderingOptions.MarginTop = 20;
            renderer.RenderingOptions.MarginBottom = 20;
            renderer.RenderingOptions.MarginLeft = 20;
            renderer.RenderingOptions.MarginRight = 20;
            renderer.RenderingOptions.CreatePdfFormsFromHtml = true;
            
            // Generar PDF
            var pdf = renderer.RenderHtmlAsPdf(html);
            pdf.SaveAs(outputPath);
        }
    }
}
