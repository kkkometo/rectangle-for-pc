# Compila Encuadre.exe con el compilador C# incluido en Windows (.NET Framework 4.8).
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$src = Get-ChildItem (Join-Path $root 'src') -Filter *.cs | ForEach-Object { $_.FullName }
$obj = Join-Path $root 'obj'
New-Item -ItemType Directory -Force $obj | Out-Null
$refs = '/r:System.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll'
$common = @('/nologo', '/target:winexe', '/optimize+', '/codepage:65001', '/platform:anycpu') + $refs

# 1) compilacion previa para generar el icono
$tmp = Join-Path $obj 'Encuadre.tmp.exe'
& $csc @common "/out:$tmp" @src
if ($LASTEXITCODE -ne 0) { throw 'Error de compilacion' }
$ico = Join-Path $obj 'Encuadre.ico'
Start-Process -FilePath $tmp -ArgumentList '--write-icon', "`"$ico`"" -Wait

# 2) compilacion final con icono
$out = Join-Path $root 'Encuadre.exe'
& $csc @common "/win32icon:$ico" "/out:$out" @src
if ($LASTEXITCODE -ne 0) { throw 'Error de compilacion' }
Remove-Item $tmp -Force
Write-Output "Compilado: $out"
