<#
.SYNOPSIS
  Build + unit test + cai add-in "Camera ao" vao RobotStudio 2026 bang MOT lenh.

.EXAMPLE
  .\build.ps1                      # build Release, chay test, cai add-in
.EXAMPLE
  .\build.ps1 -BoQuaTest -KhongCai # chi build
.EXAMPLE
  .\build.ps1 -RobotStudioBin "C:\Program Files (x86)\ABB\RobotStudio 2026\Bin"

.NOTES
  - Can .NET 10 SDK va RobotStudio 2026 (SDK nam trong thu muc Bin cua RobotStudio).
  - Phai DONG RobotStudio truoc khi cai (RobotStudio khoa file DLL cua add-in).
  - Add-in duoc chep vao <RobotStudio>\Bin\Addins\CameraAo (nhom Users co quyen ghi, khong can admin).
#>
param(
    [string]$RobotStudioBin = "",
    [string]$CauHinh = "config\CAM1.json",
    [ValidateSet('Debug', 'Release')][string]$CheDo = 'Release',
    [switch]$BoQuaTest,
    [switch]$KhongCai
)
$ErrorActionPreference = 'Stop'
$goc = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $goc

function Find-RobotStudioBin {
    if ($RobotStudioBin) { return $RobotStudioBin }
    if ($env:ROBOTSTUDIO_BIN) { return $env:ROBOTSTUDIO_BIN }
    $khoa = 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*',
            'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*'
    foreach ($k in (Get-ItemProperty $khoa -ErrorAction SilentlyContinue)) {
        if ($k.DisplayName -like 'ABB RobotStudio 2026*' -and $k.InstallLocation) {
            $p = Join-Path $k.InstallLocation 'Bin'
            if (Test-Path (Join-Path $p 'ABB.Robotics.RobotStudio.dll')) { return $p }
        }
    }
    foreach ($p in @("${env:ProgramFiles(x86)}\ABB\RobotStudio 2026\Bin", 'D:\Program Files (x86)\ABB\RobotStudio 2026\Bin')) {
        if (Test-Path (Join-Path $p 'ABB.Robotics.RobotStudio.dll')) { return $p }
    }
    throw 'Khong tim thay RobotStudio 2026. Dung tham so -RobotStudioBin "<duong dan>\Bin".'
}

function Test-RsAddinSchema([string]$file, [string]$xsd) {
    $set = New-Object System.Xml.Schema.XmlSchemaSet
    $set.XmlResolver = $null
    [void]$set.Add('urn:abb-robotics-robotstudio-addin', $xsd)
    $s = New-Object System.Xml.XmlReaderSettings
    $s.ValidationType = 'Schema'
    $s.Schemas = $set
    $script:loiSchema = @()
    $s.add_ValidationEventHandler({ param($o, $e) $script:loiSchema += $e.Message })
    $r = [System.Xml.XmlReader]::Create($file, $s)
    while ($r.Read()) { }
    $r.Close()
    return $script:loiSchema
}

$rs = Find-RobotStudioBin
Write-Host "== RobotStudio SDK: $rs" -ForegroundColor Cyan

# 1. Build ca solution
Write-Host "== Build ($CheDo)" -ForegroundColor Cyan
& dotnet build CameraAo.sln -c $CheDo "-p:RSBin=$rs" -nologo
if ($LASTEXITCODE -ne 0) { throw "Build loi (ma $LASTEXITCODE)" }

# 2. Unit test (mo hinh camera + truyen du lieu, khong can RobotStudio)
if (-not $BoQuaTest) {
    Write-Host "== Unit test" -ForegroundColor Cyan
    & dotnet test tests\CameraAo.Model.Tests\CameraAo.Model.Tests.csproj -c $CheDo --no-build -nologo
    if ($LASTEXITCODE -ne 0) { throw "Unit test loi (ma $LASTEXITCODE)" }
}

# 3. Kiem tra file .rsaddin theo schema cua RobotStudio
$rsaddin = 'src\CameraAo.Addin\CameraAo.rsaddin'
$xsd = Join-Path (Split-Path $rs -Parent) 'Schemas\RobotStudioAddin.xsd'
if (Test-Path $xsd) {
    $loi = Test-RsAddinSchema $rsaddin $xsd
    if ($loi.Count -gt 0) { throw ("CameraAo.rsaddin khong hop le: " + ($loi -join '; ')) }
    Write-Host "== CameraAo.rsaddin hop le theo schema" -ForegroundColor Green
}

# 4. Cai add-in
if ($KhongCai) { Write-Host "== Bo qua buoc cai (-KhongCai)." -ForegroundColor Yellow; exit 0 }
if (Get-Process RobotStudio -ErrorAction SilentlyContinue) {
    throw 'RobotStudio dang chay -> hay luu station, DONG RobotStudio roi chay lai build.ps1.'
}
$ra = Join-Path $goc "src\CameraAo.Addin\bin\$CheDo\net10.0-windows"
$dich = Join-Path $rs 'Addins\CameraAo'
New-Item -ItemType Directory -Force $dich | Out-Null
Copy-Item (Join-Path $ra '*') $dich -Recurse -Force
$fileCauHinh = (Resolve-Path $CauHinh).Path
Set-Content -Path (Join-Path $dich 'duong_dan_cau_hinh.txt') -Value $fileCauHinh -Encoding ASCII
Write-Host "== Da cai add-in vao: $dich" -ForegroundColor Green
Write-Host "   File cau hinh: $fileCauHinh"
Get-ChildItem $dich -File | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize
Write-Host "Mo RobotStudio -> mo station -> tab 'Camera ao'." -ForegroundColor Green
