# Usage: powershell -File visual_overhaul_ablation.ps1 <build> <run> [<vo-off groups>]
# Same Player arguments as RunCombatLoad.ps1 (1920x1080 exclusive, D3D11) with 5 cycles; accepts another resolution
# (logged in <run>-display.txt) because ablation compares B against itself. Adds --vo-off for VisualOverhaulAblation.
param([string]$Build,[string]$Run,[string]$Off='')
$project=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$exe=Join-Path $project "Tools/IronRequiem/Builds/CombatLoad/$Build/IronRequiemCombatLoad.exe"
$out=Join-Path $project "Tools/IronRequiem/Reports/CombatLoad/$Run"
$a=@('-force-d3d11','-screen-width','1920','-screen-height','1080','-screen-fullscreen','1','-window-mode','exclusive','--iron-output',$out,'--iron-cycles','5','--iron-profile','0','--iron-any-resolution','1','-logFile',"$out-player.log")
if($Off){$a+=@('--vo-off',$Off)}
$p=Start-Process -FilePath $exe -ArgumentList $a -PassThru
$t=New-Object System.Collections.Generic.List[object]; $w=[Diagnostics.Stopwatch]::StartNew()
while(!$p.HasExited){$t.Add([pscustomobject]@{seconds=$w.Elapsed.TotalSeconds;gpu=(nvidia-smi --query-gpu=memory.used,utilization.gpu,temperature.gpu,power.draw,pstate,clocks.current.graphics --format=csv,noheader,nounits | Out-String).Trim()}); if($w.Elapsed.TotalSeconds -gt 400){$p.CloseMainWindow()|Out-Null;break}; Start-Sleep 5}
$t | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath "$out-system.json"
"$Run exit=$($p.ExitCode) done=$(Test-Path "$out.done")"
