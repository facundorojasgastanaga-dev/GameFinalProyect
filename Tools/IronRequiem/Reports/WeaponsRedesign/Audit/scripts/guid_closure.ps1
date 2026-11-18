# Read-only: transitive closure of assets referencing a GUID (prefab nesting/variants, scenes, assets).
param([string[]]$Seeds)
Set-Location (Split-Path -Parent $PSScriptRoot | Split-Path -Parent | Split-Path -Parent | Split-Path -Parent | Split-Path -Parent | Split-Path -Parent)
$seen=@{}; $queue=New-Object System.Collections.Queue; $edges=@()
foreach($s in $Seeds){$queue.Enqueue($s); $seen[$s]='SEED'}
while($queue.Count){ $g=$queue.Dequeue()
  $files = git grep -l --untracked -e $g -- 'Assets/*.prefab' 'Assets/*.unity' 'Assets/*.asset' 'Assets/*.cs'
  foreach($f in $files){ $edges += [pscustomobject]@{from=$g;file=$f}
    if($f -like '*.prefab'){ $mg=((Get-Content "$f.meta" | Select-String '^guid:').Line -replace 'guid: ','').Trim(); if(-not $seen.ContainsKey($mg)){$seen[$mg]=$f; $queue.Enqueue($mg)} } } }
$edges | ConvertTo-Json -Depth 3
