$ErrorActionPreference = 'Stop'
try {
    $package = Split-Path -Parent $MyInvocation.MyCommand.Path
    $src = Join-Path $package 'WiFiToggle.cs'
    if (-not (Test-Path $src)) { throw 'WiFiToggle.cs puuttuu. Pura ZIP kokonaan.' }
    $candidates = @(
        (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'),
        (Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe')
    )
    $csc = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $csc) { throw '.NET Framework 4.x -kääntäjää ei löytynyt.' }
    $folder = Join-Path $env:LOCALAPPDATA 'Programs\WiFiToggle'
    New-Item -ItemType Directory -Force -Path $folder | Out-Null
    $exe = Join-Path $folder 'WiFiToggle.exe'
    Get-CimInstance Win32_Process -Filter "Name = 'WiFiToggle.exe'" -ErrorAction SilentlyContinue |
        Where-Object { $_.ExecutablePath -eq $exe } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
    & $csc /nologo /target:winexe /optimize+ ("/out:" + $exe) /reference:System.Windows.Forms.dll /reference:System.Drawing.dll $src
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $exe)) { throw 'Kääntäminen epäonnistui.' }
    $shell = New-Object -ComObject WScript.Shell
    function Shortcut {
        param(
            [Parameter(Mandatory=$true)][string]$ShortcutPath,
            [Parameter(Mandatory=$true)][string]$LaunchMode,
            [Parameter(Mandatory=$true)][string]$Description
        )
        $shortcut = $shell.CreateShortcut($ShortcutPath)
        $shortcut.TargetPath = $exe
        $shortcut.Arguments = [string]$LaunchMode
        $shortcut.Description = $Description
        $shortcut.WorkingDirectory = $folder
        $shortcut.IconLocation = "$exe,0"
        $shortcut.Save()
    }
    Shortcut (Join-Path ([Environment]::GetFolderPath('Desktop')) 'Wi-Fi-vaihtaja.lnk') '--toggle' 'Vaihda Wi-Fi'
    Shortcut (Join-Path ([Environment]::GetFolderPath('Programs')) 'Wi-Fi-vaihtaja.lnk') '--toggle' 'Vaihda Wi-Fi'
    Shortcut (Join-Path ([Environment]::GetFolderPath('Programs')) 'Vaihda Wi-Fi-vaihtajan verkot.lnk') '--configure' 'Valitse verkot'
    Shortcut (Join-Path ([Environment]::GetFolderPath('Startup')) 'Wi-Fi-vaihtajan ilmaisin.lnk') '--tray' 'Näytä verkon tila'
    Write-Host 'Valitse kaksi tallennettua verkkoa.'
    Start-Process -FilePath $exe -ArgumentList '--configure' -Wait
    Start-Process -FilePath $exe -ArgumentList '--tray'
    Write-Host 'Asennus valmis. Etsi Käynnistä-valikosta Wi-Fi-vaihtaja ja kiinnitä tehtäväpalkkiin.'
} catch { Write-Host ('ASENNUS EPÄONNISTUI: ' + $_.Exception.Message) -ForegroundColor Red; exit 1 }
