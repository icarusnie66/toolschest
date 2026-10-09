$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceRoot = Join-Path $projectRoot 'src'
$outputRoot = Join-Path $projectRoot 'outputs'
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'

if (-not (Test-Path -LiteralPath $compiler)) {
    $compiler = 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $compiler)) {
    throw '未找到 Windows .NET Framework C# 编译器。'
}

New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
$sources = Get-ChildItem -LiteralPath $sourceRoot -Filter '*.cs' | ForEach-Object FullName
$ocrResource = Join-Path $projectRoot 'work\tesseract-runtime.zip'
if (-not (Test-Path -LiteralPath $ocrResource)) { throw '缺少内置 OCR 运行时资源。' }
$resourceArgument = '/resource:{0},ToolChest.TesseractRuntime' -f $ocrResource
& $compiler /nologo /target:winexe /optimize+ /out:"$outputRoot\工具宝匣.exe" `
    /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll /reference:Microsoft.VisualBasic.dll `
    /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll `
    $resourceArgument $sources
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "生成完成：$outputRoot\工具宝匣.exe"

