# 부기 빌드 스크립트
# .NET SDK 의 Roslyn(csc.dll)로 .NET Framework 4.8 대상 단일 EXE 를 만든다. (설치 없이 EXE 한 개로 실행)
param([string]$Out = "$PSScriptRoot\output\bugi.exe")
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$dotnet = (Get-Command dotnet).Source
$csc = Get-ChildItem "$env:USERPROFILE\.dotnet\sdk\*\Roslyn\bincore\csc.dll", "$env:LOCALAPPDATA\Microsoft\dotnet\sdk\*\Roslyn\bincore\csc.dll", 'C:\Program Files\dotnet\sdk\*\Roslyn\bincore\csc.dll' -ErrorAction SilentlyContinue |
    Sort-Object FullName -Descending | Select-Object -First 1
if (-not $csc) { throw 'Roslyn csc.dll 을 찾지 못했어요. .NET SDK 를 설치해 주세요.' }
$fw = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$refs = 'mscorlib', 'System', 'System.Core', 'System.Drawing', 'System.Windows.Forms', 'System.Xml', 'System.Xml.Linq'
New-Item -ItemType Directory -Force (Split-Path $Out), "$root\obj" | Out-Null

function Invoke-Csc([string]$target, [string]$icon) {
    $rsp = New-Object System.Collections.Generic.List[string]
    $rsp.Add('/noconfig'); $rsp.Add('/nostdlib+'); $rsp.Add('/target:winexe'); $rsp.Add('/optimize+'); $rsp.Add('/platform:anycpu')
    $rsp.Add('/nowarn:CS0649,CS0169,CS0414,CS0219'); $rsp.Add('/utf8output'); $rsp.Add('/langversion:latest')
    $rsp.Add('/out:"' + $target + '"'); $rsp.Add('/win32manifest:"' + "$root\assets\app.manifest" + '"')
    if ($icon -and (Test-Path $icon)) { $rsp.Add('/win32icon:"' + $icon + '"') }
    foreach ($r in $refs) { $rsp.Add('/r:"' + "$fw\$r.dll" + '"') }
    Get-ChildItem "$root\src\*.cs" | ForEach-Object { $rsp.Add('"' + $_.FullName + '"') }
    $rspPath = "$root\obj\build.rsp"
    [IO.File]::WriteAllLines($rspPath, $rsp, (New-Object Text.UTF8Encoding($true)))
    & $dotnet $csc.FullName "@$rspPath"
    if ($LASTEXITCODE -ne 0) { throw "빌드 실패 (exit $LASTEXITCODE)" }
}

# 아이콘이 없으면: 아이콘 없이 한 번 빌드해 실행 파일이 그린 얼굴로 .ico 를 만든 뒤, 다시 빌드한다.
$icon = "$root\assets\bugi.ico"
if (-not (Test-Path $icon)) {
    $tmp = "$root\obj\icon-maker.exe"
    Invoke-Csc $tmp $null
    Start-Process -FilePath $tmp -ArgumentList '--make-icon', ('"' + $icon + '"') -Wait
    if (-not (Test-Path $icon)) { throw '아이콘 생성 실패' }
}
Invoke-Csc $Out $icon
Write-Host ("빌드 완료: {0} ({1:N0} bytes)" -f $Out, (Get-Item $Out).Length)

# ───── 가이드북 점검 (docs/GUIDE_RULES.md) ─────
# 1) 캐릭터 그리기 코드에 숫자로 쓴 색은 출처(//g:쪽수 가이드북, //fx 표정 효과, //ref 가이드북에 없는 참조 그림)를 줄 끝에 적어야 한다.
$untagged = Get-ChildItem "$root\src\Rig*.cs" | ForEach-Object {
    $f = $_; $n = 0
    Get-Content $f.FullName -Encoding UTF8 | ForEach-Object { $n++; if ($_ -match 'Color\.FromArgb\([^)]*\d+\s*,\s*\d+\s*,\s*\d+' -and $_ -notmatch '//\s*(g:|fx|ref)') { "{0}:{1}: {2}" -f $f.Name, $n, $_.Trim() } }
}
if ($untagged) {
    Write-Host "경고: 가이드북 전용색상이 아닌 색 리터럴에 출처 표기(//g:쪽수, //fx, //ref)가 없어요 — 가이드북(p12)을 먼저 확인하세요." -ForegroundColor Yellow
    $untagged | ForEach-Object { Write-Host "  $_" -ForegroundColor Yellow }
}
# 2) 그려 본 캐릭터가 가이드북 색·선·비율을 지키는지 (bugi.exe --guide-test)
$gOut = Join-Path $env:TEMP 'bugi_guide_out.txt'
$proc = Start-Process -FilePath $Out -ArgumentList '--guide-test' -Wait -PassThru -RedirectStandardOutput $gOut -WindowStyle Hidden
$gText = if (Test-Path $gOut) { Get-Content $gOut -Encoding UTF8 } else { @() }
foreach ($line in $gText) { if ($line -match 'WARNING|※') { Write-Host $line -ForegroundColor Yellow } else { Write-Host $line -ForegroundColor DarkGray } }
if ($proc.ExitCode -eq 2) { Write-Host '⚠ 가이드북에 어긋나는 항목이 있어요. 위 경고를 확인해 주세요.' -ForegroundColor Yellow }
