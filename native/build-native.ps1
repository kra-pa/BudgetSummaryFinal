$ErrorActionPreference = 'Stop'
gcc -shared -O2 -o "$PSScriptRoot\budget_native.dll" "$PSScriptRoot\budget_native.c"
Write-Host "Built native\budget_native.dll (Windows x64/UCRT64)."
