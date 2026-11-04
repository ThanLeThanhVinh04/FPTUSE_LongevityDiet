param([string]$Path = (Join-Path $PSScriptRoot '..\docs\assignment\extended-diagrams\05-c2-component-architecture.drawio'))
$ErrorActionPreference='Stop'
[xml]$xml=Get-Content -LiteralPath $Path -Raw -Encoding utf8
$errors=0;$count=0
function New-Waypoint($doc,$list,$x,$y){
  $p=$doc.CreateElement('mxPoint')
  $p.SetAttribute('x',[string][int][math]::Round($x))
  $p.SetAttribute('y',[string][int][math]::Round($y))
  [void]$list.AppendChild($p)
}
foreach($page in $xml.SelectNodes('/mxfile/diagram')){
  $model=$page.SelectSingleNode('mxGraphModel')
  $model.SetAttribute('pageHeight','1210')
  $root=$model.SelectSingleNode('root')
  $verts=@{}
  foreach($cell in $root.SelectNodes('mxCell[@vertex="1"]')){
    $verts[$cell.GetAttribute('id')]=$cell
    if($cell.GetAttribute('value').StartsWith('C4 Level 3 / COMPONENT VIEW')){
      $cell.SelectSingleNode('mxGeometry').SetAttribute('y','1150')
    }
  }
  $edgeNo=0
  foreach($edge in $root.SelectNodes('mxCell[@edge="1"]')){
    $src=$verts[$edge.GetAttribute('source')]
    $dst=$verts[$edge.GetAttribute('target')]
    if(-not ($src -and $dst)){ $errors++;continue }
    $gs=$src.SelectSingleNode('mxGeometry')
    $gt=$dst.SelectSingleNode('mxGeometry')
    $sx=[double]$gs.GetAttribute('x');$sy=[double]$gs.GetAttribute('y')
    $sw=[double]$gs.GetAttribute('width');$sh=[double]$gs.GetAttribute('height')
    $tx=[double]$gt.GetAttribute('x');$ty=[double]$gt.GetAttribute('y')
    $tw=[double]$gt.GetAttribute('width');$th=[double]$gt.GetAttribute('height')
    $rs=[int][math]::Round(($sy-204)/254)
    $rt=[int][math]::Round(($ty-204)/254)
    $cs=[int][math]::Round(($sx-70)/((1620-35*2)/3+35))
    $ct=[int][math]::Round(($tx-70)/((1620-35*2)/3+35))
    $xc=$sx+$sw/2
    $xt=$tx+$tw/2
    $geo=$edge.SelectSingleNode('mxGeometry')
    if(-not $geo){ throw "Missing geometry $($edge.GetAttribute('id'))" }
    $old=$geo.SelectSingleNode('Array')
    if($old){[void]$geo.RemoveChild($old)}
    $arr=$xml.CreateElement('Array');$arr.SetAttribute('as','points')
    $baseStyle='edgeStyle=orthogonalEdgeStyle;rounded=0;orthogonalLoop=1;jettySize=auto;html=0;strokeColor=#416887;strokeWidth=2;endArrow=block;endFill=1;fontSize=13;labelBackgroundColor=#ffffff;'
    $fraction=($edgeNo%5)*7
    if($rs -eq $rt){
      # Same-row sideways link: use horizontal lane above boxes.
      $yTop=$sy-35-$fraction
      $style=$baseStyle+'exitX=0.5;exitY=0;entryX=0.5;entryY=0;'
      New-Waypoint $xml $arr $xc $yTop
      New-Waypoint $xml $arr $xt $yTop
    }elseif([math]::Abs($rs-$rt) -eq 1){
      if($rs -lt $rt){
        $yMid=$sy+$sh+42+$fraction
        $style=$baseStyle+'exitX=0.5;exitY=1;entryX=0.5;entryY=0;'
      }else{
        $yMid=$sy-42-$fraction
        $style=$baseStyle+'exitX=0.5;exitY=0;entryX=0.5;entryY=1;'
      }
      if($cs -ne $ct){
        New-Waypoint $xml $arr $xc $yMid
        New-Waypoint $xml $arr $xt $yMid
      }
    }else{
      # Route cross-band paths around the left/right margin to avoid clipping nodes.
      $left=($xc+$xt)/2 -lt 900
      $gutter=if($left){42}else{1720}
      $departureY=$sy+$sh+40+($edgeNo%3)*7
      $arriveY=$ty-40-($edgeNo%3)*7
      if($rs -lt $rt){
        $style=$baseStyle+'exitX=0.5;exitY=1;entryX=0.5;entryY=0;'
        New-Waypoint $xml $arr $xc $departureY
        New-Waypoint $xml $arr $gutter $departureY
        New-Waypoint $xml $arr $gutter $arriveY
        New-Waypoint $xml $arr $xt $arriveY
      }else{
        $style=$baseStyle+'exitX=0.5;exitY=0;entryX=0.5;entryY=1;'
        New-Waypoint $xml $arr $xc ($sy-40)
        New-Waypoint $xml $arr $gutter ($sy-40)
        New-Waypoint $xml $arr $gutter ($ty+$th+40)
        New-Waypoint $xml $arr $xt ($ty+$th+40)
      }
    }
    $edge.SetAttribute('style',$style)
    if($arr.ChildNodes.Count -gt 0){[void]$geo.AppendChild($arr)}
    $count++;$edgeNo++
  }
}
$settings=[System.Xml.XmlWriterSettings]::new();$settings.Encoding=[System.Text.UTF8Encoding]::new($false);$settings.Indent=$true
$writer=[System.Xml.XmlWriter]::Create($Path,$settings)
try {$xml.Save($writer)} finally {$writer.Dispose()}
Write-Host "C2_ROUTED_EDGES=$count MISSING=$errors"
if($errors -gt 0){exit 1}
