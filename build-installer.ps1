param([string]$Compiler = 'ISCC.exe', [string]$PublishDirectory)
$ErrorActionPreference = 'Stop'
if (!$PublishDirectory) { $PublishDirectory = Join-Path (Split-Path $PSScriptRoot -Parent) 'Serein-Profilliste' }
$payload = (Resolve-Path -LiteralPath $PublishDirectory).Path
if (!(Test-Path -LiteralPath (Join-Path $payload 'Serein.exe'))) { throw 'Portable Serein-Ausgabe fehlt.' }
& $Compiler '/Q' ('/DAppSource=' + $payload) (Join-Path $PSScriptRoot 'installer/Serein.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer-Build fehlgeschlagen.' }
