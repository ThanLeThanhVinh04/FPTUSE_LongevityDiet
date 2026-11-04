$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
& git -C $repo add -A 2>&1 | ForEach-Object {
  if ("$_" -notmatch 'CRLF will be replaced') { Write-Host $_ }
}
if($LASTEXITCODE -ne 0){throw 'git add failed'}
# Vendored third-party skill files are preserved as upstream source. Their own
# whitespace is not rewritten; the repository-owned files must pass check.
$ps=@(
  'README.md','scripts/','src/','tests/','cloudflare/','docs/'
)
& git -C $repo diff --cached --check -- $ps
if($LASTEXITCODE -ne 0){throw 'First-party diff whitespace validation failed'}
$count=@(& git -C $repo diff --cached --name-only).Count
Write-Host "STAGED_FILES_TOTAL=$count"
$skillCount=@(& git -C $repo diff --cached --name-only -- '.agents/skills/upstream-*').Count
Write-Host "VENDORED_SKILL_FILES=$skillCount"
Write-Host 'STAGING_AND_OWNED_WHITESPACE_PASS'
