<#
.SYNOPSIS
    KiHan 逻辑层全量测试运行脚本 (PowerShell 版)
.DESCRIPTION
    编译检查 netstandard2.1 并依次执行各测试工程 (Combat, Skill, Jump, HitGC, Hurt, Stride)。
    适用于 Windows PowerShell / PowerShell Core，不需要 Unity。
.PARAMETER Project
    可选。指定仅运行单个工程，例如: -Project Combat
.PARAMETER Detailed
    可选开关。输出完整的测试执行日志，不进行摘要过滤。
.EXAMPLE
    .\run_all.ps1
.EXAMPLE
    .\run_all.ps1 -Project Combat -Detailed
#>
[CmdletBinding()]
param(
    [string]$Project = "",
    [switch]$Detailed
)
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

$ErrorActionPreference = "Continue"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
Set-Location $scriptDir

# 检查 dotnet 命令
$dotnetCmd = Get-Command "dotnet" -ErrorAction SilentlyContinue
if (-not $dotnetCmd) {
    Write-Host "[ERROR] 未找到 dotnet SDK，请确保已安装 .NET 8 SDK 并加入 PATH 环境变量。" -ForegroundColor Red
    exit 1
}

$fail = 0
$targets = @("Combat", "Skill", "Jump", "HitGC", "Hurt", "Stride")
if ($Project) {
    if ($targets -contains $Project -or $Project -eq "Compile") {
        if ($Project -eq "Compile") {
            $targets = @()
        } else {
            $targets = @($Project)
        }
    } else {
        Write-Host "[ERROR] 未知工程: $Project。可选值: Compile, Combat, Skill, Jump, HitGC, Hurt, Stride" -ForegroundColor Red
        exit 1
    }
}

# 1. 编译检查 (netstandard2.1)
if (-not $Project -or $Project -eq "Compile") {
    Write-Host "## Compile (netstandard2.1)" -ForegroundColor Cyan
    $compileOut = & dotnet build Compile/Compile.csproj -nologo -v q 2>&1
    $compileCode = $LASTEXITCODE

    if ($Detailed) {
        $compileOut | ForEach-Object { Write-Host $_ }
    } else {
        $compileOut | Where-Object { $_ -match " error | warning |Warning\(s\)|Error\(s\)" } | ForEach-Object { Write-Host $_ }
    }

    if ($compileCode -ne 0) {
        Write-Host "  [FAIL] 编译失败 (exit $compileCode)" -ForegroundColor Red
        $fail = 1
    } else {
        Write-Host "  [PASS] 编译成功" -ForegroundColor Green
    }
}

# 2. 依次运行测试工程
foreach ($t in $targets) {
    Write-Host "## $t" -ForegroundColor Cyan
    $testOut = & dotnet run --project "$t/$t.csproj" -nologo -v q 2>&1
    $testCode = $LASTEXITCODE

    $hasFailKeyword = $false
    foreach ($line in $testOut) {
        if ($Detailed) {
            Write-Host $line
        } else {
            if ($line -match "\[FAIL\]|全部通过|失败 [0-9]+ 项|same=|托管分配|Exception") {
                Write-Host $line
            }
        }
        if ($line -match "\[FAIL\]|Unhandled exception") {
            $hasFailKeyword = $true
        }
    }

    if ($testCode -ne 0 -or $hasFailKeyword) {
        Write-Host "  [FAIL] $t (exit $testCode)" -ForegroundColor Red
        $fail = 1
    } else {
        Write-Host "  [PASS] $t 通过" -ForegroundColor Green
    }
}

Write-Host "----------------------------------------------------"
if ($fail -eq 0) {
    Write-Host "ALL PASSED" -ForegroundColor Green
} else {
    Write-Host "SOME TESTS FAILED" -ForegroundColor Red
}

exit $fail
