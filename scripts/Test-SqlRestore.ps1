param(
    [Parameter(Mandatory = $true)]
    [string]$BackupPath,
    [string]$VerificationDatabase = "LongevityDietRestoreVerification",
    [switch]$KeepDatabase
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$resolvedBackup = (Resolve-Path $BackupPath).Path
$sqlContainer = (docker compose ps -q sqlserver).Trim()
if (-not $sqlContainer) { throw "SQL Server container must be running." }

$sqlPassword = (docker exec $sqlContainer sh -lc 'printenv MSSQL_SA_PASSWORD').Trim()
$containerBackup = "/var/opt/mssql/backup/restore-verification.bak"
docker exec $sqlContainer mkdir -p /var/opt/mssql/backup | Out-Null
$sqlDestination = "$($sqlContainer):$containerBackup"
docker cp $resolvedBackup $sqlDestination | Out-Null

$mdf = "/var/opt/mssql/data/$VerificationDatabase.mdf"
$ldf = "/var/opt/mssql/data/$($VerificationDatabase)_log.ldf"
$query = "IF DB_ID(N'$VerificationDatabase') IS NOT NULL BEGIN ALTER DATABASE [$VerificationDatabase] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$VerificationDatabase]; END; RESTORE DATABASE [$VerificationDatabase] FROM DISK=N'$containerBackup' WITH MOVE N'LongevityDietDb' TO N'$mdf', MOVE N'LongevityDietDb_log' TO N'$ldf', REPLACE, CHECKSUM; SELECT COUNT(*) AS MigrationCount FROM [$VerificationDatabase].[dbo].[__EFMigrationsHistory];"
docker exec $sqlContainer /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P $sqlPassword -C -b -Q $query
if ($LASTEXITCODE -ne 0) { throw "Restore verification failed." }

if (-not $KeepDatabase) {
    $cleanup = "ALTER DATABASE [$VerificationDatabase] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$VerificationDatabase];"
    docker exec $sqlContainer /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P $sqlPassword -C -b -Q $cleanup | Out-Null
}
docker exec $sqlContainer rm -f $containerBackup | Out-Null
Write-Host "RESTORE_VERIFICATION=PASS"
