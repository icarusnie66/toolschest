$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$env:TOOL_CHEST_ROOT = $projectRoot
$references = @(
    'System.dll',
    'System.Core.dll',
    'System.Drawing.dll',
    'System.Windows.Forms.dll',
    'Microsoft.VisualBasic.dll',
    'System.IO.Compression.dll',
    'System.IO.Compression.FileSystem.dll'
)
$sources = Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' | ForEach-Object FullName
Add-Type -Path $sources -ReferencedAssemblies $references
[MuxiaToolbox.Program]::Main()

