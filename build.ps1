#!/usr/bin/env pwsh
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

dotnet run --project (Join-Path $PSScriptRoot "nuke") -- @args
exit $LASTEXITCODE
