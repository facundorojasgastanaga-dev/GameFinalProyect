# Compare two protected-menu capture folders (same file names). Samples every 2nd pixel in luminance.
# Reports mean absolute difference (0-255) and % of samples differing by more than 24.
param([string]$A, [string]$B)
Add-Type -AssemblyName System.Drawing
Get-ChildItem $A -Filter *.png | Sort-Object Name | ForEach-Object {
  $ia = [System.Drawing.Bitmap]::FromFile($_.FullName); $ib = [System.Drawing.Bitmap]::FromFile((Join-Path $B $_.Name))
  $sum = 0.0; $big = 0; $n = 0
  for ($y = 0; $y -lt $ia.Height; $y += 2) { for ($x = 0; $x -lt $ia.Width; $x += 2) {
    $p = $ia.GetPixel($x, $y); $q = $ib.GetPixel($x, $y)
    $d = [math]::Abs((0.299*$p.R + 0.587*$p.G + 0.114*$p.B) - (0.299*$q.R + 0.587*$q.G + 0.114*$q.B))
    $sum += $d; if ($d -gt 24) { $big++ }; $n++ } }
  $ia.Dispose(); $ib.Dispose()
  '{0,-26} mean={1,6:N2} >24={2,6:N2}%' -f $_.Name, ($sum / $n), (100.0 * $big / $n)
}
