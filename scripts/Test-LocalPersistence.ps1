param(
    [int]$TimeoutSeconds = 90
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$sqlContainer = (docker compose ps -q sqlserver).Trim()
$redisContainer = (docker compose ps -q redis).Trim()
if (-not $sqlContainer -or -not $redisContainer) {
    throw "SQL Server and Redis containers must be running."
}

$sqlPassword = (docker exec $sqlContainer sh -lc 'printenv MSSQL_SA_PASSWORD').Trim()
$marker = "release-readiness:" + [guid]::NewGuid().ToString("N")
docker exec $redisContainer redis-cli SET release-readiness:marker $marker | Out-Null

$countBefore = (docker exec $sqlContainer /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P $sqlPassword -C -d LongevityDietDb -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM __EFMigrationsHistory;").Trim()
if (-not $countBefore) { throw "Could not read migration count before restart." }

docker compose restart sqlserver redis | Out-Null

$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
do {
    Start-Sleep -Seconds 2
    $sqlStatus = docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' $sqlContainer 2>$null
    $redisStatus = docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' $redisContainer 2>$null
} until (($sqlStatus -eq "healthy" -and $redisStatus -eq "healthy") -or (Get-Date) -gt $deadline)

if ($sqlStatus -ne "healthy" -or $redisStatus -ne "healthy") {
    throw "Persistence restart did not return services to healthy state."
}

$sqlPassword = (docker exec $sqlContainer sh -lc 'printenv MSSQL_SA_PASSWORD').Trim()
$countAfter = (docker exec $sqlContainer /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P $sqlPassword -C -d LongevityDietDb -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM __EFMigrationsHistory;").Trim()
$markerAfter = (docker exec $redisContainer redis-cli GET release-readiness:marker).Trim()

if ($countBefore -ne $countAfter) { throw "SQL migration history changed across restart." }
if ($markerAfter -ne $marker) { throw "Redis persisted marker was lost across restart." }

Write-Host "SQL_MIGRATIONS_BEFORE=$countBefore"
Write-Host "SQL_MIGRATIONS_AFTER=$countAfter"
Write-Host "REDIS_MARKER_PERSISTED=PASS"
Write-Host "NAMED_VOLUME_RESTART_PERSISTENCE=PASS"
