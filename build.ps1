param([switch]$OnlineTests)
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    $publish = Join-Path (Split-Path $PSScriptRoot -Parent) 'Serein'
    dotnet restore Serein.App/Serein.App.csproj -r win-x64 --configfile NuGet.Config
    if ($LASTEXITCODE -ne 0) { throw 'Restore fehlgeschlagen.' }
    dotnet publish Serein.App/Serein.App.csproj -c Release -r win-x64 --self-contained true --no-restore -o $publish
    if ($LASTEXITCODE -ne 0) { throw 'Publish fehlgeschlagen.' }
    Copy-Item data/autoeq-index.md,data/AutoEq-LICENSE.txt $publish
    Copy-Item PORTABLE.md (Join-Path $publish 'ANLEITUNG.md')
    Copy-Item THIRD-PARTY.md (Join-Path $publish 'Serein-THIRD-PARTY.md')
    Copy-Item LICENSE.txt (Join-Path $publish 'Serein-LICENSE.txt')
    New-Item -ItemType Directory -Force (Join-Path $publish 'docs'),(Join-Path $publish 'data') | Out-Null
    Copy-Item docs/FORMAT.md (Join-Path $publish 'docs/FORMAT.md')
    Copy-Item data/AutoEq-LICENSE.txt (Join-Path $publish 'data/AutoEq-LICENSE.txt')
    $packageRoot = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path ([Environment]::GetFolderPath('UserProfile')) '.nuget/packages' }
    foreach ($package in @('microsoft.netcore.app.runtime.win-x64','microsoft.windowsdesktop.app.runtime.win-x64')) {
        $runtime = Get-ChildItem (Join-Path $packageRoot $package) -Directory -Filter '10.*' | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
        if ($runtime) {
            Get-ChildItem $runtime.FullName -File | Where-Object { $_.Name -match '^LICENSE|THIRD-PARTY' } | ForEach-Object { Copy-Item $_.FullName (Join-Path $publish ($package + '-' + $_.Name)) }
        }
    }
    dotnet restore Serein.Tests/Serein.Tests.csproj --configfile NuGet.Config
    if ($LASTEXITCODE -ne 0) { throw 'Test-Restore fehlgeschlagen.' }
    dotnet build Serein.Tests/Serein.Tests.csproj -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Test-Build fehlgeschlagen.' }
    Copy-Item data/autoeq-index.md Serein.Tests/bin/Release/net10.0/autoeq-index.md
    $testDirectory = Join-Path $PSScriptRoot ('.test-runs/' + [Guid]::NewGuid().ToString('N'))
    if ($OnlineTests) { dotnet Serein.Tests/bin/Release/net10.0/Serein.Tests.dll $testDirectory --online }
    else { dotnet Serein.Tests/bin/Release/net10.0/Serein.Tests.dll $testDirectory }
    if ($LASTEXITCODE -ne 0) { throw 'Tests fehlgeschlagen.' }
    Write-Output "Portable Ausgabe: $publish"
} finally { Pop-Location }
