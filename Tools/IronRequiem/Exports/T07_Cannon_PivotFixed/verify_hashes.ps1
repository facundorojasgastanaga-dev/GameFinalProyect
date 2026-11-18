# Compares SHA-256 of protected sources against the values recorded before this task (2026-11-16).
$ErrorActionPreference = 'Stop'
Set-Location (Resolve-Path "$PSScriptRoot/../../../..")
$expected = [ordered]@{
  'Assets/IronRequiem/Art/Enemies/Exterminador/Models/Meshy_AI_Rustborn_Sentinel_20261028_texture.fbx'      = 'A0E48EAF7FA334AD6FECD515D6E27675E1768A9CCB9C18A813977A269FAA6238'
  'Assets/IronRequiem/Art/Enemies/Exterminador/Models/Meshy_AI_Rustborn_Sentinel_20261028_texture.fbx.meta' = '3804C94F38BE8A40995E87699603DAAF63E7C0A2814FD3B0427A9FFC2A0DCD86'
}
# Working sources and the existing equipped prefab: compared against this task's backup / Blender report.
$backup = 'Tools/IronRequiem/Backups/20261116-weapon-pivot'
$pairs = @(
  @('Assets/IronRequiem/Prefabs/Weapons/Weapon_T07_Temporary_Optimized.prefab', "$backup/Weapon_T07_Temporary_Optimized.prefab"),
  @('Assets/IronRequiem/Prefabs/Weapons/Weapon_T07_Temporary_Optimized.prefab.meta', "$backup/Weapon_T07_Temporary_Optimized.prefab.meta")
)
$cleanSha = (Get-Content 'Tools/IronRequiem/Reports/WeaponPivot/inspect-T07_CannonClean.json' -Raw | ConvertFrom-Json).source_sha256.ToUpper()
$expected['Assets/IronRequiem/Art/Weapons/T07CannonClean/T07_CannonClean.fbx'] = $cleanSha
$rows = @()
foreach ($k in $expected.Keys) {
  $h = (Get-FileHash -Algorithm SHA256 $k).Hash
  $rows += [pscustomobject]@{ File = $k; Before = $expected[$k]; After = $h; Unchanged = ($h -eq $expected[$k]) }
}
foreach ($p in $pairs) {
  $a = (Get-FileHash -Algorithm SHA256 $p[1]).Hash; $b = (Get-FileHash -Algorithm SHA256 $p[0]).Hash
  $rows += [pscustomobject]@{ File = $p[0]; Before = $a; After = $b; Unchanged = ($a -eq $b) }
}
$rows | ConvertTo-Json | Set-Content -Encoding utf8 'Tools/IronRequiem/Reports/WeaponPivot/hash-comparison.json'
$rows | Format-Table -AutoSize -Wrap
if ($rows.Unchanged -contains $false) { Write-Error 'A protected file changed.' }
