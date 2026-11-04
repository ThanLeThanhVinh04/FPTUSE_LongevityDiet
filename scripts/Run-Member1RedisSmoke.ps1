# Requires: Docker Desktop running, cached redis:7-alpine image and dotnet 9.
# Never touches application Docker volumes or real user databases.
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $repo 'scripts\Member1.RedisSmoke\Member1.RedisSmoke.csproj'
$name = 'longevity-member1-redis-smoke'
$image = 'redis:7-alpine'
$port = '16389'
$started = $false

if (-not (Test-Path -LiteralPath $project)) {
    throw "Missing Redis E2E smoke project: $project"
}
if ((Get-PSDrive -Name C).Free -lt 600MB) {
    throw 'Too little free space on drive C: even for temporary Docker Redis smoke validation.'
}
& docker image inspect $image --format '{{.Id}}' 1>$null 2>$null
if ($LASTEXITCODE -ne 0) {
    throw "Cached image $image is required; this script will not pull new images."
}
$existing = & docker ps -a --filter "name=^/$name$" --format '{{.Names}}'
if ($existing) {
    throw "Container $name already exists. Review it manually before running smoke test."
}
try {
    $mapping = ('127.0.0.1:{0}:6379' -f $port)
    & docker run --rm -d --name $name --memory=96m --cpus=1 --tmpfs /data:rw,size=16m -p $mapping $image redis-server --save '' --appendonly no
    if ($LASTEXITCODE -ne 0) { throw 'Temporary Redis container failed to start.' }
    $started = $true

    $ready = $false
    for ($attempt = 0; $attempt -lt 20; $attempt++) {
        $ping = & docker exec $name redis-cli ping 2>$null
        if ($LASTEXITCODE -eq 0 -and $ping -eq 'PONG') {
            $ready = $true
            break
        }
        Start-Sleep -Milliseconds 250
    }
    if (-not $ready) { throw 'Temporary Redis failed readiness check.' }

    $address = ('127.0.0.1:{0}' -f $port)
    & dotnet run --project $project -c Release -- $address
    if ($LASTEXITCODE -ne 0) { throw 'Redis + SQLite application E2E smoke failed.' }
    Write-Host 'MEMBER1_REDIS_SMOKE_PASS'
}
finally {
    if ($started) {
        & docker stop $name 1>$null 2>$null
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Could not stop $name. Inspect Docker state manually."
        }
        else {
            Write-Host 'TEMP_REDIS_CLEANED_UP'
        }
    }
}
