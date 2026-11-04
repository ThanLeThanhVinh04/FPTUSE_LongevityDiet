$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$uv=Join-Path (Split-Path $root -Parent) '_diagram_toolkit_sources\uv-portable-0.13.7\uv.exe'
if(-not(Test-Path -LiteralPath $uv)){ throw 'Verified portable uv is required to run the SQLite schema checks' }
$test=Join-Path $root 'cloudflare\refresh-sessions\tests\test_sql_schema.py'
& $uv run --python 3.12 python $test
if($LASTEXITCODE -ne 0){throw 'SQLite schema tests failed'}
