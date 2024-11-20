# Herramienta de Comparación DB-Entity Framework

Esta herramienta compara las tablas de una base de datos SQL Server con sus correspondientes archivos de Entity Framework, específicamente para tablas con prefijo `VT_`.

## Requisitos Previos

- .NET 8.0 SDK
- SQL Server (2016 o superior)
- Visual Studio 2022 (opcional)

## Instalación

1. Clonar el repositorio
2. Restaurar paquetes NuGet:
```bash
dotnet restore
```

## Estructura del Proyecto

```
SchemaComparison/
├── SchemaComparison.Core/
│   └── SchemaComparisonTool.cs
├── SchemaComparison.Console/
│   ├── Program.cs
│   └── Reportes/          # Carpeta donde se guardan los reportes generados
└── SchemaComparison.sln
```

## Reportes

Los reportes se generan automáticamente en la carpeta `SchemaComparison.Console/Reportes/` con el formato:
```
ReporteComparacion_AAAAMMDD_HHMMSS.txt
```
Donde:
- AAAAMMDD: fecha (ejemplo: 20241120)
- HHMMSS: hora (ejemplo: 153000)

## Configuración

1. Modificar `Program.cs` con tus credenciales:
```csharp
string connectionString = "Server=TU_SERVIDOR;Database=TU_BASE_DE_DATOS;Trusted_Connection=True;";
string entityFilesPath = @"RUTA\A\TUS\ARCHIVOS\VT_";
```

Para encontrar estos valores:

### Conexión SQL Server
1. Abrir SQL Server Management Studio
2. Click derecho en el servidor -> Properties
3. Copiar "Server Name"
4. Reemplazar en connectionString

### Ruta de Archivos Entity Framework
1. Localizar carpeta con archivos VT_*.cs
2. Copiar ruta completa
3. Reemplazar en entityFilesPath

## Uso

```bash
cd SchemaComparison.Console
dotnet run
```

El programa generará:
- Reporte en consola
- Archivo de texto con timestamp (ejemplo: `ReporteComparacion_20241120_153000.txt`)

## Estructura del Reporte

El reporte muestra:
- Diagnóstico inicial (total tablas, archivos encontrados)
- Lista de todas las tablas con:
  - ✅ Tablas congruentes
  - ❌ Tablas con diferencias
- Resumen final

## Solución de Problemas

### Error: "Could not find a part of the path..."
- Verificar que la ruta en entityFilesPath existe
- Confirmar permisos de lectura

### Error: "A network-related or instance-specific error..."
- Verificar que el servidor SQL está accesible
- Confirmar credenciales de conexión