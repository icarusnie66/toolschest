$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$componentRoot = Join-Path $projectRoot 'outputs\components'
$installer = Join-Path $componentRoot 'tesseract-ocr-w64-setup-5.4.0.20240606.exe'
if (-not (Test-Path -LiteralPath $installer)) {
    throw '未找到 Tesseract 安装包。请重新获取完整输出文件。'
}
Write-Host '正在安装 Tesseract OCR，请稍候……'
$arguments = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-')
$process = Start-Process -FilePath $installer -ArgumentList $arguments -Wait -PassThru
if ($process.ExitCode -ne 0) { throw "Tesseract 安装失败，退出码：$($process.ExitCode)" }
$tesseract = Join-Path $env:ProgramFiles 'Tesseract-OCR\tesseract.exe'
if (-not (Test-Path -LiteralPath $tesseract)) { throw '安装结束，但未找到 tesseract.exe。' }
& $tesseract --version
Write-Host 'Tesseract OCR 安装完成。工具宝匣将使用项目内的中英文语言模型。'
Read-Host '按回车键关闭'
