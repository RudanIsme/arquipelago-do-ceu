@echo off
rem Os mesmos portões de qualidade da CI, num comando só. Uso: tools\qualidade.cmd [base-dos-commits]
setlocal
cd /d "%~dp0.."
set DOTNET_NOLOGO=1
if "%~1"=="" (set BASE=origin/main) else (set BASE=%~1)

echo == Formatação
for %%P in (core\ArquipelagoDoCeu.Core.csproj game.tests\ArquipelagoDoCeu.Tests.csproj game\ArquipelagoDoCeu.csproj) do (
  dotnet format whitespace %%P --verify-no-changes || exit /b 1
)

echo == Análise estática e dependências (aviso vira erro)
dotnet build game\ArquipelagoDoCeu.sln --no-incremental || exit /b 1
dotnet build game.tests\ArquipelagoDoCeu.Tests.csproj --no-incremental || exit /b 1

echo == Testes e cobertura do core
dotnet test game.tests -p:CollectCoverage=true "-p:Include=[ArquipelagoDoCeu.Core]*" -p:Threshold=90 -p:ThresholdType=line || exit /b 1

echo == Comentários
node tools\check_comments.mjs || exit /b 1

echo == Commits
node tools\check_commits.mjs %BASE% || exit /b 1

echo.
echo Todos os portões passaram.
