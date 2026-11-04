param([string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path)
$ErrorActionPreference = 'Stop'
$paths = @(
  (Join-Path $Root 'docs/assignment/extended-diagrams/05-c2-component-architecture.drawio'),
  (Join-Path $Root 'docs/assignment/extended-diagrams/06-all-database-erd.drawio')
)
$failed = 0
foreach ($p in $paths) {
  if (-not (Test-Path -LiteralPath $p)) { Write-Host "FAIL: Missing $p"; $failed++; continue }
  try { [xml]$xml = Get-Content -LiteralPath $p -Raw -Encoding UTF8 } catch { Write-Host "FAIL: XML parse $p : $_"; $failed++; continue }
  $pages = @($xml.mxfile.diagram)
  $cells = 0
  $invalidEdges = 0
  $outOfBounds = 0
  foreach ($page in $pages) {
    $ids = @{}
    foreach ($c in @($page.mxGraphModel.root.mxCell)) { if ($c.id) { $ids[$c.id] = $true } }
    foreach ($c in @($page.mxGraphModel.root.mxCell)) {
      $cells++
      if ($c.edge -eq '1') {
        if (-not ($ids.ContainsKey([string]$c.source) -and $ids.ContainsKey([string]$c.target))) { $invalidEdges++ }
      }
      if ($c.vertex -eq '1') {
        $geo = $c.mxGeometry
        if ($null -ne $geo) {
          $x = [double]$geo.x; $y = [double]$geo.y
          $w = [double]$geo.width; $h = [double]$geo.height
          $pw = [double]$page.mxGraphModel.pageWidth; $ph = [double]$page.mxGraphModel.pageHeight
          if ($x -lt 0 -or $y -lt 0 -or $x+$w -gt $pw -or $y+$h -gt $ph) { $outOfBounds++ }
        }
      }
    }
  }
  Write-Host "FILE: $(Split-Path $p -Leaf) | Pages: $($pages.Count) | Cells: $cells | Broken edges: $invalidEdges | Out of bounds: $outOfBounds"
  foreach ($page in $pages) { Write-Host "  - $($page.name)" }
  if ($invalidEdges -gt 0 -or $outOfBounds -gt 0) { $failed++ }
  if ($p -like '*05-c2*' -and $pages.Count -ne 8) { $failed++ }
  if ($p -like '*06-all*' -and $pages.Count -ne 9) { $failed++ }
}
# Semantic QA: verify every canonical table and every local FK connector is present.
$erd = (Join-Path $Root 'docs/assignment/extended-diagrams/06-all-database-erd.drawio')
[xml]$d = Get-Content -LiteralPath $erd -Raw -Encoding UTF8
$expectedTables = @(
  'Users','UserProfiles','RefreshTokens','Allergies','UserAllergies','UserExcludedFoods','Reminders',
  'Foods','Recipes','RecipeIngredients','RecipeAllergens','DietRules','RuleVersions','RuleSetVersions','RuleSetVersionItems',
  'MealPlans','MealPlanDays','PlannedMeals','Challenges','ChallengeDays','FmdSafetyAssessments','FmdCycles',
  'MealLogs','MealLogItems','ActivityLogs','EatingWindowSnapshots','AdherenceScores','ScoreDimensions','WeeklyReports',
  'RecommendationFeedback','ProcessedEvents','OutboxMessages','AuditLogs'
)
$actualTables = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$targetPages = @($d.SelectNodes('/mxfile/diagram[starts-with(@name,"TARGET ")]'))
foreach($pg in $targetPages) {
  foreach($c in $pg.SelectNodes('./mxGraphModel/root/mxCell[@vertex="1"]')) {
    $name = $c.GetAttribute('value') -replace '\s*\[legacy\]$', ''
    if ($name -in $expectedTables) { [void]$actualTables.Add($name) }
  }
}
$missing=@($expectedTables | Where-Object { -not $actualTables.Contains($_) })
$links=@($d.SelectNodes('//mxCell[starts-with(@id,"erd-rel-")]'))
Write-Host "SEMANTIC: target databases=$($targetPages.Count) / 6, canonical tables=$($actualTables.Count) / 33, relation connectors=$($links.Count) / 20"
if ($targetPages.Count -ne 6 -or $missing.Count -ne 0 -or $actualTables.Count -ne 33 -or $links.Count -ne 20) {
  Write-Host "MISSING TABLES: $($missing -join ', ')"
  $failed++
}
if ($failed -gt 0) { Write-Error "Diagram validation failed: $failed"; exit 1 }
Write-Host 'PASS: XML, page counts, 33/33 logical target tables, 20 FK connectors, edge references, page geometry'
