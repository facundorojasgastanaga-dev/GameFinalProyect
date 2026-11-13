# Iron Requiem - git setup, paso 1 (no hace commit)
Set-Location "C:\Users\gehbd\ProyectoGameFinal"
$out = "Tools\IronRequiem\Reports\git_setup_1.txt"
& {
  "=== versiones"; git --version; git lfs version; codex --version
  "=== repo padre antes de init (debe ser C:/Users/gehbd si el home es repo)"; git rev-parse --show-toplevel
  if (-not (Test-Path ".git")) { git init -b main } else { "ya existe .git en el proyecto" }
  git config core.quotepath false
  git config user.name "Facundo"
  git config user.email "facundorojasgastanaga@gmail.com"
  "=== raiz del repo ahora"; git rev-parse --show-toplevel
  "=== carpetas pesadas"
  foreach ($d in "Assets","Packages","ProjectSettings","Docs","Tools","Tools\IronRequiem\Backups","Tools\IronRequiem\Quarantine","Tools\IronRequiem\Bridge","Tools\IronRequiem\Builds","ProfilerCaptures","farmacia-inventario",".agents") {
    if (Test-Path $d) { $s = (Get-ChildItem $d -Recurse -File -Force -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum; "{0,-32} {1,10:N1} MB" -f $d, ($s/1MB) }
  }
  "farmacia-inventario tiene .git: " + (Test-Path "farmacia-inventario\.git")
  "=== lo que se va a commitear"
  $list = git ls-files -o --exclude-standard
  $sum = 0; $rows = @()
  foreach ($f in $list) { $len = (Get-Item -LiteralPath $f -Force).Length; $sum += $len; $rows += [pscustomobject]@{MB=[math]::Round($len/1MB,1); File=$f} }
  "archivos: {0}   total: {1:N1} MB" -f $list.Count, ($sum/1MB)
  "--- mayores de 50 MB"; $rows | Where-Object MB -gt 50 | Sort-Object MB -Descending | Format-Table -AutoSize | Out-String -Width 300
  "--- 25 mas grandes"; $rows | Sort-Object MB -Descending | Select-Object -First 25 | Format-Table -AutoSize | Out-String -Width 300
  "--- por extension (MB)"; $rows | Group-Object { [IO.Path]::GetExtension($_.File).ToLower() } | ForEach-Object { [pscustomobject]@{Ext=$_.Name; N=$_.Count; MB=[math]::Round(($_.Group | Measure-Object MB -Sum).Sum,1)} } | Sort-Object MB -Descending | Select-Object -First 15 | Format-Table -AutoSize | Out-String -Width 200
} *>&1 | Tee-Object -FilePath $out
"Listo. Resultado en $out"
