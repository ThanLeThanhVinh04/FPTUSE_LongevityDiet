$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$zip=Join-Path (Split-Path $root -Parent) 'LongevityDiet_C2_AllDatabases_2026-11-05.zip'
$source=@(
  (Join-Path $root 'docs\assignment\extended-diagrams'),
  (Join-Path $root 'docs\assignment\06-C2-AND-DATABASE-MAPPING.md'),
  (Join-Path $root 'docs\assignment\07-UPSTREAM-DIAGRAM-TOOLKIT.md'),
  (Join-Path $root 'docs\assignment\08-C2-DATABASE-CLOUDFLARE-DELIVERY-REPORT.md'),
  (Join-Path $root 'docs\security\CLOUDFLARE-REFRESH-TOKENS.md'),
  (Join-Path $root 'cloudflare\refresh-sessions'),
  (Join-Path $root 'src\LongevityDiet.API\Cloudflare')
)
Compress-Archive -LiteralPath $source -DestinationPath $zip -CompressionLevel Optimal -Force
Add-Type -AssemblyName System.IO.Compression
$archive=[IO.Compression.ZipFile]::OpenRead($zip)
try {
  $names=@($archive.Entries|ForEach-Object FullName)
  $drawio=@($names|Where-Object{$_ -like '*.drawio'})
  $pdf=@($names|Where-Object{$_ -like '*.pdf'})
  $png=@($names|Where-Object{$_ -like '*.png'})
  $dotnet=@($names|Where-Object{$_ -like '*CloudflareRefreshTokenRepository.cs'})
  Write-Host "DELIVERABLE_ZIP=$zip bytes=$((Get-Item $zip).Length)"
  Write-Host "ZIP_CONTENT files=$($names.Count) drawio=$($drawio.Count) pdf=$($pdf.Count) png=$($png.Count) dotnet=$($dotnet.Count)"
  if($drawio.Count -ne 2 -or $pdf.Count -ne 2 -or $png.Count -ne 17 -or $dotnet.Count -ne 1){throw 'Deliverable ZIP verification failed'}
}finally{$archive.Dispose()}
Write-Host 'ZIP_VERIFIED'
