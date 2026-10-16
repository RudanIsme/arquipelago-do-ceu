# Compila o C# e abre o jogo. Argumentos extras vão para o jogo, depois do `--`:
#   .\tools\run.cmd
#   .\tools\run.cmd --driver=movimento --saida=$env:TEMP\capturas
param([Parameter(ValueFromRemainingArguments)] [string[]] $JogoArgs)

$ErrorActionPreference = 'Stop'
$raiz = Split-Path $PSScriptRoot -Parent

# O dotnet de "C:\Program Files" costuma vir primeiro no PATH e pode ser só o runtime, sem SDK;
# por isso o script procura um dotnet que tenha a pasta sdk.
$candidatos = @($env:DOTNET_ROOT, 'C:\dev\tools\dotnet', "$env:USERPROFILE\.dotnet", 'C:\Program Files\dotnet')
$dotnetRoot = $candidatos | Where-Object { $_ -and (Test-Path "$_\sdk") } | Select-Object -First 1
if (-not $dotnetRoot) { Write-Error 'Nenhum .NET SDK 8 encontrado. Instale o SDK ou defina DOTNET_ROOT.' }
$env:DOTNET_ROOT = $dotnetRoot
$env:PATH = "$dotnetRoot;$env:PATH"

if (-not $env:GODOT -or -not (Test-Path $env:GODOT)) {
    Write-Error 'A variável GODOT precisa apontar para o executável de console do Godot 4.7.2 (.NET).'
}

dotnet build "$raiz\game\ArquipelagoDoCeu.csproj" --nologo -v q
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$extra = if ($JogoArgs) { @('--') + $JogoArgs } else { @() }
& $env:GODOT --path "$raiz\game" @extra
exit $LASTEXITCODE
