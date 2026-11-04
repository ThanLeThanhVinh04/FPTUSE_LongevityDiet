$ErrorActionPreference='Stop'
$root=(Resolve-Path (Split-Path -Parent $MyInvocation.MyCommand.Path)).Path
$projectRoot=(Resolve-Path (Join-Path $root '..\..\..')).Path
$exe=Join-Path (Split-Path $projectRoot -Parent) '_diagram_toolkit_sources\drawio-portable-32.6.1\draw.io.exe'
if(-not(Test-Path -LiteralPath $exe)){ throw 'Verified draw.io Desktop portable is required' }
$preview=Join-Path $root 'previews'
New-Item -ItemType Directory -Path $preview -Force | Out-Null
$jobs=@(
  @{Name='05-c2-component-architecture'; Count=8},
  @{Name='06-all-database-erd'; Count=9}
)
foreach($job in $jobs){
  $source=Join-Path $root ($job.Name+'.drawio')
  for($page=1;$page -le $job.Count;$page++){
    $file=Join-Path $preview ($job.Name+'-p'+$page.ToString('00')+'.png')
    Remove-Item -LiteralPath $file -ErrorAction SilentlyContinue -Force
    & $exe --export --format png --theme light --size page --page-index $page --output $file $source
    for($tick=0;$tick -lt 100 -and -not(Test-Path -LiteralPath $file);$tick++){ Start-Sleep -Milliseconds 200 }
    if(-not(Test-Path -LiteralPath $file)){ throw "PNG failed $($job.Name) page $page" }
    $length=(Get-Item -LiteralPath $file).Length
    if($length -lt 8000){ throw "PNG suspiciously small: $file $length bytes" }
    Write-Host "PNG PASS $($job.Name) page $page bytes $length"
  }
  $pdf=Join-Path $root ($job.Name+'.pdf')
  Remove-Item -LiteralPath $pdf -ErrorAction SilentlyContinue -Force
  & $exe --export --format pdf --all-pages --theme light --output $pdf $source
  for($tick=0;$tick -lt 130 -and -not(Test-Path -LiteralPath $pdf);$tick++){Start-Sleep -Milliseconds 250}
  if(-not(Test-Path -LiteralPath $pdf)){ throw "PDF failed $($job.Name)" }
  Write-Host "PDF PASS $pdf bytes $((Get-Item $pdf).Length)"
}
Write-Host 'RENDER_EXPORT_ALL_PASS'
