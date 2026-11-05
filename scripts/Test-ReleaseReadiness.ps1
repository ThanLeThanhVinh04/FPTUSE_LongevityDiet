param(
    [switch]$RequireRunningStack
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

& "$PSScriptRoot\Validate-ProjectStructure.ps1"
if (-not $?) { throw "Project structure validation failed." }

docker compose --env-file .env.example config --quiet
if ($LASTEXITCODE -ne 0) { throw "Docker Compose config validation failed." }

$configJson = docker compose --env-file .env.example config --format json | ConvertFrom-Json
foreach ($serviceName in @("sqlserver", "redis", "recommendation-grpc", "api", "web")) {
    $service = $configJson.services.$serviceName
    foreach ($port in @($service.ports)) {
        if ($null -ne $port -and $port.host_ip -ne "127.0.0.1") {
            throw "$serviceName publishes a port outside loopback: $($port.host_ip)"
        }
    }
}

$ignoredEnv = git check-ignore .env
if ($ignoredEnv -ne ".env") { throw ".env must remain ignored by Git." }

$trackedSecrets = git ls-files .env "*.bak" "*.rdb"
if ($trackedSecrets) { throw "Tracked secret/backup artifacts detected: $trackedSecrets" }

if ($RequireRunningStack) {
    $required = @("sqlserver", "redis", "recommendation-grpc", "api", "worker", "web")
    foreach ($serviceName in $required) {
        $id = (docker compose ps -q $serviceName).Trim()
        if (-not $id) { throw "$serviceName is not running." }
    }

    $redis = (docker compose ps -q redis).Trim()
    if ((docker exec $redis redis-cli PING).Trim() -ne "PONG") { throw "Redis PING failed." }

    $apiStatus = (Invoke-WebRequest -UseBasicParsing -Uri "http://127.0.0.1:8080/health").StatusCode
    $webStatus = (Invoke-WebRequest -UseBasicParsing -Uri "http://127.0.0.1:5173/").StatusCode
    if ($apiStatus -ne 200 -or $webStatus -ne 200) { throw "API/Web health smoke failed." }

    $sqlVolume = docker volume inspect longevity-diet-sqlserver-data --format '{{.Mountpoint}}'
    $redisVolume = docker volume inspect longevity-diet-redis-data --format '{{.Mountpoint}}'
    if (-not $sqlVolume -or -not $redisVolume) { throw "Named volume mountpoint evidence missing." }

    Write-Host "API_HEALTH=200"
    Write-Host "WEB_HEALTH=200"
    Write-Host "REDIS_PING=PONG"
    Write-Host "SQL_VOLUME=$sqlVolume"
    Write-Host "REDIS_VOLUME=$redisVolume"
}

Write-Host "RELEASE_READINESS_STATIC=PASS"
