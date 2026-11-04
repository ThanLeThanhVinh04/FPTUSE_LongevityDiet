param(
    [Parameter(Mandatory=$true)][string]$BaseUrl,
    [ValidateSet('/health','/ready','/api/v1/activities','/api/v1/notifications')]
    [string]$Path='/health',
    [ValidateRange(10,1000)][int]$Iterations=100,
    [string]$OutputPath=''
)
$ErrorActionPreference='Stop'
$uri=[Uri]::new($BaseUrl.TrimEnd('/')+$Path)
if($uri.Scheme -ne 'https' -and $uri.Host -notin @('localhost','127.0.0.1','::1')){
    throw 'Only HTTPS or loopback HTTP is supported for performance checks.'
}
if($Path -like '/api/*' -and [string]::IsNullOrWhiteSpace($env:LONGEVITY_BENCHMARK_ACCESS_TOKEN)){
    throw 'Set LONGEVITY_BENCHMARK_ACCESS_TOKEN privately when benchmarking protected endpoints.'
}
$root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if(-not $OutputPath){$OutputPath=Join-Path $root 'docs\task\member1-runtime-benchmark.json'}
$client=[System.Net.Http.HttpClient]::new()
$client.Timeout=[TimeSpan]::FromSeconds(12)
if($Path -like '/api/*') {
    $client.DefaultRequestHeaders.Authorization=
        [System.Net.Http.Headers.AuthenticationHeaderValue]::new(
            'Bearer',$env:LONGEVITY_BENCHMARK_ACCESS_TOKEN)
}
$metrics=New-Object 'System.Collections.Generic.List[double]'
$failures=0
$rejected=0
$watch=[Diagnostics.Stopwatch]::StartNew()
try{
    for($i=0;$i -lt $Iterations;$i++){
        $timer=[Diagnostics.Stopwatch]::StartNew()
        try{
            $response=$client.GetAsync($uri).GetAwaiter().GetResult()
            $timer.Stop()
            $metrics.Add($timer.Elapsed.TotalMilliseconds)
            if(-not $response.IsSuccessStatusCode) {
                $failures++
                if([int]$response.StatusCode -eq 429){$rejected++}
            }
            $response.Dispose()
        }catch{
            $timer.Stop()
            $metrics.Add($timer.Elapsed.TotalMilliseconds)
            $failures++
        }
    }
}finally{$watch.Stop();$client.Dispose()}
$sorted=@($metrics|Sort-Object)
function Percentile([double[]]$values,[double]$percent){
    $idx=[math]::Ceiling(($values.Length*$percent)/100)-1
    return [math]::Round($values[[math]::Max(0,$idx)],2)
}
$result=[ordered]@{
    timestampUtc=[DateTime]::UtcNow.ToString('o')
    endpoint=$uri.GetLeftPart([UriPartial]::Path)
    iterations=$Iterations
    p50Ms=(Percentile $sorted 50)
    p95Ms=(Percentile $sorted 95)
    p99Ms=(Percentile $sorted 99)
    meanMs=[math]::Round(($sorted|Measure-Object -Average).Average,2)
    failures=$failures
    rateLimited=$rejected
    failurePercent=[math]::Round(100*$failures/$Iterations,2)
    elapsedSeconds=[math]::Round($watch.Elapsed.TotalSeconds,2)
    environment='live-target; hardware/load characteristics must be documented separately'
}
($result|ConvertTo-Json -Depth 4)|Set-Content -LiteralPath $OutputPath -Encoding UTF8
$result|Format-List|Out-String|Write-Host
Write-Host "SAVED_EVIDENCE=$OutputPath"
if($failures -gt 0){exit 1}
