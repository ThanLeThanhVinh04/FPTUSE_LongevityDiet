param(
    [string]$OutputDirectory = ".local-backups"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$destination = Join-Path $root $OutputDirectory
New-Item -ItemType Directory -Force -Path $destination | Out-Null

$sqlContainer = (docker compose ps -q sqlserver).Trim()
$redisContainer = (docker compose ps -q redis).Trim()
if (-not $sqlContainer -or -not $redisContainer) {
    throw "SQL Server and Redis containers must be running."
}

$sqlPassword = (docker exec $sqlContainer sh -lc 'printenv MSSQL_SA_PASSWORD').Trim()
if (-not $sqlPassword) { throw "MSSQL_SA_PASSWORD is unavailable inside the SQL container." }

$containerBackupDir = "/var/opt/mssql/backup"
$containerBackup = "$containerBackupDir/LongevityDietDb-$stamp.bak"
docker exec $sqlContainer mkdir -p $containerBackupDir | Out-Null

$query = "BACKUP DATABASE [LongevityDietDb] TO DISK = N'$containerBackup' WITH COPY_ONLY, INIT, CHECKSUM; RESTORE VERIFYONLY FROM DISK = N'$containerBackup' WITH CHECKSUM;"
docker exec $sqlContainer /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P $sqlPassword -C -b -Q $query
if ($LASTEXITCODE -ne 0) { throw "SQL backup or RESTORE VERIFYONLY failed." }

$sqlLocal = Join-Path $destination "LongevityDietDb-$stamp.bak"
$sqlSource = "$($sqlContainer):$containerBackup"
docker cp $sqlSource $sqlLocal | Out-Null
docker exec $sqlContainer rm -f $containerBackup | Out-Null

docker exec $redisContainer redis-cli SAVE | Out-Null
$redisLocal = Join-Path $destination "redis-$stamp.rdb"
$redisSource = "$($redisContainer):/data/dump.rdb"
docker cp $redisSource $redisLocal | Out-Null

Write-Host "SQL_BACKUP=$sqlLocal"
Write-Host "REDIS_SNAPSHOT=$redisLocal"
Write-Host "BACKUP_VERIFY=PASS"
