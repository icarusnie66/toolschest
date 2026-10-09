$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$bundledPython = 'C:\Users\26576\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
$pythonExe = if (Test-Path -LiteralPath $bundledPython) { $bundledPython } else { 'python' }
$venvPython = Join-Path $projectRoot '.venv\Scripts\python.exe'
$pipCache = Join-Path $projectRoot 'work\pip-cache'
New-Item -ItemType Directory -Force -Path $pipCache | Out-Null

if (-not (Test-Path -LiteralPath $venvPython)) {
    & $pythonExe -m venv (Join-Path $projectRoot '.venv')
}

& $venvPython -m pip install --cache-dir $pipCache --upgrade pip
if ($LASTEXITCODE -ne 0) { throw 'pip 更新失败。' }
& $venvPython -m pip install --cache-dir $pipCache -r (Join-Path $projectRoot 'requirements.txt')
if ($LASTEXITCODE -ne 0) { throw '增强组件安装失败。' }
& $venvPython -c "import pyftpdlib, faster_whisper; print('pyftpdlib=ok'); print('faster_whisper=ok')"
if ($LASTEXITCODE -ne 0) { throw '组件导入验证失败。' }
Write-Host '增强组件安装并验证完成。请运行 run.ps1。'

