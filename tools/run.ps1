# Compila o C# e abre o jogo. Argumentos extras vão para o jogo, depois do `--`:
#   .\tools\run.cmd
#   .\tools\run.cmd --driver=movimento --saida=$env:TEMP\capturas
param([Parameter(ValueFromRemainingArguments)] [string[]] $JogoArgs)

$ErrorActionPreference = 'Stop'
$raiz = Split-Path $PSScriptRoot -Parent

if (-not $env:GODOT -or -not (Test-Path $env:GODOT)) {
    Write-Error 'A variável GODOT precisa apontar para o executável de console do Godot 4.7.2 (.NET).'
}

dotnet build "$raiz\game\ArquipelagoDoCeu.csproj" --nologo -v q
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$extra = if ($JogoArgs) { @('--') + $JogoArgs } else { @() }
& $env:GODOT --path "$raiz\game" @extra
exit $LASTEXITCODE
