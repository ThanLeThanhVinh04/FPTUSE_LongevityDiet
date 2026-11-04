$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$diagramDir = Join-Path $root 'docs\assignment\diagrams'
$previewDir = Join-Path $root 'docs\assignment\previews'
$vectorDir = Join-Path $root 'docs\assignment\vector'
$drawio = 'D:\Draw.io\draw.io.exe'
if (-not (Test-Path -LiteralPath $drawio)) {
    $portable = Join-Path (Split-Path $root -Parent) '_diagram_toolkit_sources\drawio-portable-32.6.1\draw.io.exe'
    if (Test-Path -LiteralPath $portable) { $drawio = $portable }
}
if (-not (Test-Path -LiteralPath $drawio)) {
    throw "Draw.io Desktop is required to render Assignment diagrams."
}

function Invoke-DrawIoExport {
    param(
        [string]$Format,
        [string]$InputFile,
        [string]$OutputFile,
        [int]$Width = 0
    )

    for ($attempt = 1; $attempt -le 2; $attempt++) {
        Remove-Item $OutputFile -Force -ErrorAction SilentlyContinue

        if ($Format -eq 'png') {
            $arguments = @(
                '--export', '--format', 'png', '--theme', 'light', '--size', 'page',
                '--width', [string]$Width, '--output', ('"' + $OutputFile + '"'), ('"' + $InputFile + '"')
            )
        }
        else {
            $arguments = @(
                '--export', '--format', 'svg', '--theme', 'light',
                '--output', ('"' + $OutputFile + '"'), ('"' + $InputFile + '"')
            )
        }

        $process = Start-Process -FilePath $drawio -ArgumentList $arguments -PassThru -WindowStyle Hidden
        $finished = $process.WaitForExit(30000)
        if (-not $finished) {
            try { $process.Kill() } catch { }
        }

        for ($i = 0; $i -lt 10 -and -not (Test-Path $OutputFile); $i++) {
            Start-Sleep -Milliseconds 250
        }

        if ((Test-Path $OutputFile) -and (Get-Item $OutputFile).Length -gt 0) {
            return
        }

        if ($attempt -lt 2) { Start-Sleep -Seconds 1 }
    }

    throw "draw.io $Format export failed or timed out after retry: $([IO.Path]::GetFileName($InputFile))"
}
$expected = @(
    '01-c0-system-context.drawio',
    '02-c1-container-architecture.drawio',
    '03-conceptual-erd.drawio',
    '04-physical-database.drawio'
)

function Assert-OneGlanceDiagramReadability {
    param(
        [string]$File,
        [string]$Name
    )

    if ($Name -notin @('01-c0-system-context.drawio', '02-c1-container-architecture.drawio')) {
        return
    }

    [xml]$diagramXml = Get-Content $File -Raw -Encoding UTF8
    $model = $diagramXml.mxfile.diagram.mxGraphModel
    $pageWidth = [int]$model.pageWidth
    $pageHeight = [int]$model.pageHeight
    $aspect = $pageWidth / [double]$pageHeight
    $rawDiagram = Get-Content $File -Raw -Encoding UTF8
    $fontSizes = @()
    foreach ($match in [regex]::Matches($rawDiagram, '(?:fontSize=|font-size:)(\d+)')) {
        $fontSizes += [int]$match.Groups[1].Value
    }
    if ($fontSizes.Count -eq 0) {
        throw "No explicit typography found for one-glance validation: $Name"
    }
    $minFont = ($fontSizes | Measure-Object -Minimum).Minimum

    if ($Name -eq '01-c0-system-context.drawio') {
        if ($pageWidth -gt 1900 -or $pageHeight -gt 1100) {
            throw "C0 one-glance page is oversized: ${pageWidth}x${pageHeight}; target <= 1900x1100."
        }
        if ($minFont -lt 13) {
            throw "C0 typography below readability floor: ${minFont}px; minimum is 13px."
        }
    }
    else {
        if ($pageWidth -gt 3000 -or $pageHeight -gt 1800) {
            throw "C1 one-glance page is oversized: ${pageWidth}x${pageHeight}; target <= 3000x1800."
        }
        if ($minFont -lt 12) {
            throw "C1 typography below readability floor: ${minFont}px; minimum is 12px."
        }
    }

    if ($aspect -lt 1.55 -or $aspect -gt 1.95) {
        throw "One-glance landscape aspect is outside presentation range in ${Name}: $([math]::Round($aspect, 2))."
    }

    Write-Output "PASS one-glance readability $Name (${pageWidth}x${pageHeight}, min ${minFont}px)"
}

$geometryLint = Join-Path $root 'scripts\Lint-ArchitectureGeometry.ps1'
& $geometryLint -ArchitectureDir $diagramDir

New-Item -ItemType Directory -Force -Path $previewDir | Out-Null
New-Item -ItemType Directory -Force -Path $vectorDir | Out-Null

foreach ($name in $expected) {
    $file = Join-Path $diagramDir $name

    if (-not (Test-Path $file)) {
        throw "Missing diagram: $name"
    }

    $raw = Get-Content $file -Raw -Encoding UTF8

    try {
        [xml]$xml = $raw
    }
    catch {
        throw "Invalid draw.io XML: $name"
    }

    if (@($xml.mxfile.diagram).Count -lt 1) {
        throw "No draw.io page found: $name"
    }

    Assert-OneGlanceDiagramReadability -File $file -Name $name

    foreach ($edge in $xml.SelectNodes('//mxCell[@edge="1"]')) {
        $style = [string]$edge.style
        if ($style -notmatch 'edgeStyle=orthogonalEdgeStyle') {
            throw "Non-orthogonal connector detected in $name (edge $($edge.id))"
        }
        if ($style -notmatch 'curved=0') {
            throw "Curved connector is not explicitly disabled in $name (edge $($edge.id))"
        }
        if ($style -notmatch 'endArrow=block' -or $style -notmatch 'endFill=1' -or $style -notmatch 'endSize=12') {
            throw "Arrowhead style is inconsistent in $name (edge $($edge.id))"
        }
    }

    if ($raw -match '[^\x00-\x7F]') {
        throw "Possible encoding artifact in $name"
    }

    $png = Join-Path $previewDir ([IO.Path]::GetFileNameWithoutExtension($name) + '.png')
    $exportWidth = switch ($name) {
        '01-c0-system-context.drawio' { 4800 }
        '02-c1-container-architecture.drawio' { 6000 }
        '03-conceptual-erd.drawio' { 6500 }
        '04-physical-database.drawio' { 6000 }
        default { 4800 }
    }
    Invoke-DrawIoExport -Format 'png' -InputFile $file -OutputFile $png -Width $exportWidth

    if (-not (Test-Path $png)) {
        throw "PNG export failed: $name"
    }

    $svg = Join-Path $vectorDir ([IO.Path]::GetFileNameWithoutExtension($name) + '.svg')
    Invoke-DrawIoExport -Format 'svg' -InputFile $file -OutputFile $svg
    if (-not (Test-Path $svg)) {
        throw "SVG export failed: $name"
    }

    Write-Output "PASS $name (PNG + SVG)"
}

$sectionPreviewScript = Join-Path $root 'scripts\Generate-PhysicalDbSectionPreviews.py'
$pythonCommand = Get-Command python -ErrorAction SilentlyContinue
if ($pythonCommand) {
    & $pythonCommand.Source $sectionPreviewScript
    if ($LASTEXITCODE -ne 0) {
        throw 'Physical DB section preview generation failed with Python.'
    }
}
else {
    $uvCommand = Get-Command uv -ErrorAction SilentlyContinue
    $uvPath = if ($uvCommand) { $uvCommand.Source } else { Join-Path $env:USERPROFILE '.local\bin\uv.exe' }
    if (-not (Test-Path $uvPath)) {
        $portableUv = Join-Path (Split-Path $root -Parent) '_diagram_toolkit_sources\uv-portable-0.13.7\uv.exe'
        if (Test-Path -LiteralPath $portableUv) { $uvPath = $portableUv }
    }
    if (-not (Test-Path $uvPath)) {
        throw 'Neither Python nor uv is available; physical DB section preview regeneration is mandatory.'
    }
    & $uvPath run --with pillow --python 3.12 python $sectionPreviewScript
    if ($LASTEXITCODE -ne 0) {
        throw 'Physical DB section preview generation failed with uv + Pillow.'
    }
}

$sectionDir = Join-Path $previewDir 'physical-db-sections'
foreach ($sectionFile in @(
    '01-identity-profile.png',
    '02-catalog-rules.png',
    '03-planning-tracking.png',
    '04-progress-engagement-safety.png',
    '05-messaging-reliability-operations.png'
)) {
    $sectionPath = Join-Path $sectionDir $sectionFile
    if (-not (Test-Path $sectionPath) -or (Get-Item $sectionPath).Length -le 0) {
        throw "Required regenerated section preview is missing or empty: $sectionFile"
    }
}
Write-Output 'PASS Physical DB section preview regeneration'

$assignmentDoc = Join-Path $root 'docs\assignment\00-ASSIGNMENT-DOCUMENT.md'
if (-not (Test-Path $assignmentDoc)) {
    throw 'Missing 00-ASSIGNMENT-DOCUMENT.md'
}
$assignmentText = Get-Content $assignmentDoc -Raw -Encoding UTF8
foreach ($requiredSection in @(
    '## 1. Context',
    '## 2. Problems',
    '## 3. Solutions',
    '## 4. Main Actors',
    '## 5. Main Features',
    '## 6. System Architecture - C4',
    '## 7. Technology',
    '## 8. ERD - Conceptual',
    '## 9. Physical Database',
    '## 10. PRN232 Assignment Coverage'
)) {
    if ($assignmentText -notmatch [regex]::Escape($requiredSection)) {
        throw "Missing Assignment document section: $requiredSection"
    }
}
foreach ($requiredTerm in @(
    'ASP.NET Core .NET 9',
    'EF Core 9',
    'SQL Server 2022',
    'JWT',
    'Redis 7 Streams',
    'gRPC',
    'Background Worker',
    'Docker Compose',
    'OpenAPI'
)) {
    if ($assignmentText -notmatch [regex]::Escape($requiredTerm)) {
        throw "Missing mandatory technology/requirement in Assignment document: $requiredTerm"
    }
}
Write-Output 'PASS Assignment document required sections/technology checks'

$physicalFile = Join-Path $diagramDir '04-physical-database.drawio'
$physicalText = Get-Content $physicalFile -Raw -Encoding UTF8
$requiredTables = @(
    'Users','UserProfiles','RefreshTokens','Allergies','UserAllergies','UserExcludedFoods',
    'Foods','Recipes','RecipeIngredients','RecipeAllergens','DietRules','RuleVersions','RuleSetVersions','RuleSetVersionItems',
    'MealPlans','MealPlanDays','PlannedMeals','MealLogs','MealLogItems','ActivityLogs','EatingWindowSnapshots',
    'AdherenceScores','ScoreDimensions','Challenges','ChallengeDays','RecommendationFeedback',
    'FmdSafetyAssessments','FmdCycles','Reminders','WeeklyReports',
    'OutboxMessages','ProcessedEvents','AuditLogs'
)
foreach ($table in $requiredTables) {
    if ($physicalText -notmatch [regex]::Escape($table)) {
        throw "Missing target MVP table in Physical DB diagram: $table"
    }
}
Write-Output "PASS Physical DB full target table coverage ($($requiredTables.Count)/33)"

$c0Text = Get-Content (Join-Path $diagramDir '01-c0-system-context.drawio') -Raw -Encoding UTF8
$c1Text = Get-Content (Join-Path $diagramDir '02-c1-container-architecture.drawio') -Raw -Encoding UTF8
$c1SemanticText = [System.Net.WebUtility]::HtmlDecode($c1Text)

foreach ($requiredC0 in @('Guest','Member','Administrator','Longevity Diet Companion','Local AI Runtime','Software System')) {
    if ($c0Text -notmatch [regex]::Escape($requiredC0)) {
        throw "Missing required C0 element/content: $requiredC0"
    }
}
foreach ($requiredC1 in @(
    'Target Architecture','API Gateway','Identity & Profile Service','Catalog & Rules Service','Planning Service','Tracking & Progress Service',
    'Recommendation Service','Background Worker','Event Streams',
    'LongevityIdentityDb','LongevityCatalogDb','LongevityPlanningDb','LongevityTrackingDb','LongevityRecommendationDb','LongevityWorkerDb',
    ':443',':8080',':8081',':8082',':8083',':8084',':8085',':8086',':6379',':1433',':11434',
    'gRPC / HTTP/2','Redis Streams','EF Core / TDS'
)) {
    if ($c1SemanticText -notmatch [regex]::Escape($requiredC1)) {
        throw "Missing required Target C1 element/content: $requiredC1"
    }
}
foreach ($sampleOnly in @('Mobile App','Cloudinary','Brevo','RabbitMQ','Google AI')) {
    if ($c0Text -match [regex]::Escape($sampleOnly) -or $c1SemanticText -match [regex]::Escape($sampleOnly)) {
        throw "Reference-image component leaked into project architecture: $sampleOnly"
    }
}
foreach ($forbiddenC0 in @('React 19','ASP.NET Core','SQL Server 2022','Redis 7 Streams','gRPC / HTTP2','Local LLM / Ollama-style HTTP API')) {
    if ($c0Text -match [regex]::Escape($forbiddenC0)) {
        throw "Implementation detail leaked into C4 System Context: $forbiddenC0"
    }
}
foreach ($forbiddenC1 in @('React 19 + TypeScript + Vite + Nginx','SQL Server 2022 + EF Core migrations','[Container - Message Broker]')) {
    if ($c1SemanticText -match [regex]::Escape($forbiddenC1)) {
        throw "C4 abstraction leak detected in Container view: $forbiddenC1"
    }
}
foreach ($requiredTitle in @('C0 - C4 System Context','C1 - C4 Container')) {
    if ($c0Text -notmatch [regex]::Escape($requiredTitle) -and $c1SemanticText -notmatch [regex]::Escape($requiredTitle)) {
        throw "Missing course/C4 terminology disambiguation: $requiredTitle"
    }
}
Write-Output 'PASS C0/C1 project-scope, abstraction, terminology and anti-copy checks'

$dsl = Join-Path $root 'docs\assignment\c4\workspace.dsl'
if (-not (Test-Path $dsl)) {
    throw 'Missing Structurizr workspace.dsl'
}

$dslText = Get-Content $dsl -Raw -Encoding UTF8
foreach ($required in @('systemContext ldc "C0-SystemContext"', 'container ldc "C1-Container"', 'targetGateway', 'identityService', 'catalogService', 'planningService', 'trackingService', 'targetRecommendation', 'targetWorker', 'targetEventStreams', 'identityService -> identityDb', 'catalogService -> catalogDb', 'planningService -> planningDb', 'trackingService -> trackingDb', 'targetRecommendation -> recommendationDb', 'targetWorker -> workerDb', 'targetGateway -> identityService', 'targetGateway -> catalogService', 'targetGateway -> planningService', 'targetGateway -> trackingService', 'planningService -> targetRecommendation', 'Local-Demo-Deployment', 'productionTarget = deploymentEnvironment "Production Target"', 'deployment ldc productionTarget "Production-Secure-Deployment"')) {
    if ($dslText -notmatch [regex]::Escape($required)) {
        throw "Missing required C4 DSL content: $required"
    }
}

Write-Output 'PASS workspace.dsl structural checks'

$dockerCommand = Get-Command docker -ErrorAction SilentlyContinue
if (-not $dockerCommand) {
    throw 'Docker CLI is required for mandatory live Structurizr validation.'
}

$previousPreference = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
try {
    docker info *> $null
    $dockerDaemonReady = ($LASTEXITCODE -eq 0)
}
finally {
    $ErrorActionPreference = $previousPreference
}
if (-not $dockerDaemonReady) {
    throw 'Docker daemon is not available; live Structurizr validation is mandatory.'
}

$structurizrImage = 'structurizr/structurizr:latest'
$previousPreference = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
try {
    docker image inspect $structurizrImage *> $null
    $structurizrImageReady = ($LASTEXITCODE -eq 0)
}
finally {
    $ErrorActionPreference = $previousPreference
}
if (-not $structurizrImageReady) {
    docker pull $structurizrImage
    if ($LASTEXITCODE -ne 0) {
        throw 'Structurizr Docker image is required and could not be pulled.'
    }
}

$c4Dir = Split-Path $dsl -Parent
docker run --rm -v "${c4Dir}:/workspace" $structurizrImage validate -workspace /workspace/workspace.dsl
if ($LASTEXITCODE -ne 0) {
    throw 'Structurizr CLI validation failed.'
}
Write-Output 'PASS Structurizr CLI workspace validation'
Write-Output 'Assignment diagram validation complete.'
