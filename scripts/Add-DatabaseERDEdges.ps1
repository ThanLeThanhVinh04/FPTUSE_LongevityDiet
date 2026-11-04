# Idempotently add ONLY same-database FK connectors, with routing outside table bodies.
param([string]$Diagram = (Join-Path $PSScriptRoot '..\docs\assignment\extended-diagrams\06-all-database-erd.drawio'))
$ErrorActionPreference='Stop'
$edges=@{
'CURRENT SQL - DEPLOYED'=@(@('Users','UserProfiles'),@('Users','RefreshTokens'))
'TARGET 1/6 - IDENTITY & PROFILE SQL'=@(
  @('Users','UserProfiles'),@('Users','RefreshTokens [legacy]'),@('Users','UserAllergies'),
  @('Allergies','UserAllergies'),@('Users','UserExcludedFoods'),@('Users','Reminders'))
'TARGET 2/6 - CATALOG & RULES SQL'=@(
  @('Foods','RecipeIngredients'),@('Recipes','RecipeIngredients'),@('Recipes','RecipeAllergens'),
  @('DietRules','RuleVersions'),@('RuleVersions','RuleSetVersionItems'),@('RuleSetVersions','RuleSetVersionItems'))
'TARGET 3/6 - PLANNING SQL'=@(
  @('MealPlans','MealPlanDays'),@('MealPlanDays','PlannedMeals'),
  @('Challenges','ChallengeDays'),@('FmdSafetyAssessments','FmdCycles'))
'TARGET 4/6 - TRACKING & PROGRESS SQL'=@(
  @('MealLogs','MealLogItems'),@('AdherenceScores','ScoreDimensions'))
}
[xml]$doc=Get-Content -LiteralPath $Diagram -Raw -Encoding UTF8
$counter=0
foreach($page in $doc.SelectNodes('/mxfile/diagram')) {
  $pageName=$page.GetAttribute('name').Replace([char]0x2014,'-').Replace([char]0x2013,'-')
  if(-not $edges.ContainsKey($pageName)){continue}
  $root=$page.SelectSingleNode('mxGraphModel/root')
  # Remove relationships from any previous run, preserving source table cards.
  foreach($old in @($root.SelectNodes('mxCell[starts-with(@id,"erd-rel-")]'))){ [void]$root.RemoveChild($old) }
  $nodes=@{};$vertices=@($root.SelectNodes('mxCell[@vertex="1"]'))
  foreach($cell in $vertices) {
    $value=$cell.GetAttribute('value')
    if ($value -in @('Users','UserProfiles','RefreshTokens','RefreshTokens [legacy]','Allergies','UserAllergies','UserExcludedFoods','Reminders','Foods','Recipes','RecipeIngredients','RecipeAllergens','DietRules','RuleVersions','RuleSetVersions','RuleSetVersionItems','MealPlans','MealPlanDays','PlannedMeals','Challenges','ChallengeDays','FmdSafetyAssessments','FmdCycles','MealLogs','MealLogItems','AdherenceScores','ScoreDimensions')) {$nodes[$value]=$cell}
  }
  foreach($relation in $edges[$pageName]){
    $a=[string]$relation[0];$b=[string]$relation[1]
    if(-not $nodes.ContainsKey($a) -or -not $nodes.ContainsKey($b)){throw "Unknown relationship $a --> $b on page $pageName"}
    $srcTitle=$nodes[$a];$dstTitle=$nodes[$b]
    $gs=$srcTitle.SelectSingleNode('mxGeometry');$gt=$dstTitle.SelectSingleNode('mxGeometry')
    $sx=[double]$gs.GetAttribute('x');$sy=[double]$gs.GetAttribute('y');$sw=[double]$gs.GetAttribute('width')
    $tx=[double]$gt.GetAttribute('x');$ty=[double]$gt.GetAttribute('y');$tw=[double]$gt.GetAttribute('width')
    $rowA=[int][math]::Round(($sy-219)/400);$rowB=[int][math]::Round(($ty-219)/400)
    # Body cell is a separate draw.io vertex 58px below the table header.
    $srcBody=$vertices|Where-Object{ $_.SelectSingleNode('mxGeometry').GetAttribute('x') -eq $gs.GetAttribute('x') -and
      [double]$_.SelectSingleNode('mxGeometry').GetAttribute('y') -eq $sy+58 }|Select-Object -First 1
    $dstBody=$vertices|Where-Object{ $_.SelectSingleNode('mxGeometry').GetAttribute('x') -eq $gt.GetAttribute('x') -and
      [double]$_.SelectSingleNode('mxGeometry').GetAttribute('y') -eq $ty+58 }|Select-Object -First 1
    if(-not ($srcBody -and $dstBody)){throw "Could not locate body cards $a -> $b"}
    if($rowA -eq $rowB -and $rowA -eq 0){
       $src=$srcTitle;$dst=$dstTitle;$y=182+($counter%4)*7
       $port='exitX=0.5;exitY=0;entryX=0.5;entryY=0;'
    }elseif($rowA -eq $rowB){
       $src=$srcBody;$dst=$dstBody;$y=991+($counter%4)*13
       $port='exitX=0.5;exitY=1;entryX=0.5;entryY=1;'
    }elseif($rowA -lt $rowB){
       $src=$srcBody;$dst=$dstTitle;$y=580+($counter%4)*11
       $port='exitX=0.5;exitY=1;entryX=0.5;entryY=0;'
    }else{
       $src=$srcTitle;$dst=$dstBody;$y=580+($counter%4)*11
       $port='exitX=0.5;exitY=0;entryX=0.5;entryY=1;'
    }
    $edge=$doc.CreateElement('mxCell')
    $edge.SetAttribute('id','erd-rel-'+$counter.ToString('000'))
    $edge.SetAttribute('edge','1');$edge.SetAttribute('parent','1')
    $edge.SetAttribute('source',$src.GetAttribute('id'));$edge.SetAttribute('target',$dst.GetAttribute('id'))
    $edge.SetAttribute('style',"edgeStyle=orthogonalEdgeStyle;rounded=0;orthogonalLoop=1;jettySize=auto;strokeColor=#4c7399;strokeWidth=2;startArrow=ERone;endArrow=ERmany;startFill=0;endFill=0;$port")
    $geo=$doc.CreateElement('mxGeometry');$geo.SetAttribute('relative','1');$geo.SetAttribute('as','geometry')
    $arr=$doc.CreateElement('Array');$arr.SetAttribute('as','points')
    foreach($xx in @(($sx+$sw/2),($tx+$tw/2))){
       $pt=$doc.CreateElement('mxPoint');$pt.SetAttribute('x',([math]::Round($xx,2)).ToString([cultureinfo]::InvariantCulture))
       $pt.SetAttribute('y',([math]::Round($y,2)).ToString([cultureinfo]::InvariantCulture));[void]$arr.AppendChild($pt)
    }
    [void]$geo.AppendChild($arr);[void]$edge.AppendChild($geo);[void]$root.AppendChild($edge);$counter++
  }
}
$settings=[System.Xml.XmlWriterSettings]::new();$settings.Encoding=[System.Text.UTF8Encoding]::new($false);$settings.Indent=$true
$writer=[System.Xml.XmlWriter]::Create($Diagram,$settings);try{$doc.Save($writer)}finally{$writer.Dispose()}
Write-Host "ERD_LOCAL_FK_CONNECTORS_ADDED=$counter"
