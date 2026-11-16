# Genera docs/demo.gif: compila el renderizador, dibuja los fotogramas y los convierte con ffmpeg.
# Requiere ffmpeg (winget install Gyan.FFmpeg).
param([string]$Ffmpeg = '')
$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot)

if (-not $Ffmpeg) {
    $cmd = Get-Command ffmpeg -ErrorAction SilentlyContinue
    if ($cmd) { $Ffmpeg = $cmd.Source }
    else { $Ffmpeg = Get-ChildItem "$env:LOCALAPPDATA\Microsoft\WinGet\Packages" -Recurse -Filter ffmpeg.exe -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty FullName }
}
if (-not $Ffmpeg) { throw 'No se encontro ffmpeg' }

$obj = Join-Path $root 'obj'
$frames = Join-Path $obj 'demo-frames'
New-Item -ItemType Directory -Force $obj, $frames | Out-Null

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$src = @(Get-ChildItem (Join-Path $root 'src') -Filter *.cs | ForEach-Object { $_.FullName }) + (Join-Path $PSScriptRoot 'Demo.cs')
$exe = Join-Path $obj 'demo.exe'
& $csc /nologo /target:exe /optimize+ /codepage:65001 /platform:anycpu /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /main:Encuadre.Demo.DemoProgram "/out:$exe" @src
if ($LASTEXITCODE -ne 0) { throw 'Error de compilacion' }

& $exe $frames
if ($LASTEXITCODE -ne 0) { throw 'Error al dibujar los fotogramas' }

$palette = Join-Path $obj 'demo-palette.png'
$gif = Join-Path $root 'docs\demo.gif'
$in = Join-Path $frames 'frame_%05d.png'
& $Ffmpeg -hide_banner -loglevel error -y -framerate 25 -i $in -vf 'palettegen=max_colors=256:stats_mode=full' $palette
& $Ffmpeg -hide_banner -loglevel error -y -framerate 25 -i $in -i $palette -lavfi 'paletteuse=dither=bayer:bayer_scale=3:diff_mode=rectangle' -loop 0 $gif
if ($LASTEXITCODE -ne 0) { throw 'Error de ffmpeg' }
Write-Output ("Listo: {0} ({1:N1} MB)" -f $gif, ((Get-Item $gif).Length / 1MB))
