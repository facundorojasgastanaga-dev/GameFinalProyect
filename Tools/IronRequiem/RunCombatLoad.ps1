param([ValidatePattern('^[a-z0-9-]+$')][string]$Build='before-release',[ValidatePattern('^[a-z0-9-]+$')][string]$Run='before',[ValidateRange(1,15)][int]$Cycles=15,[switch]$Profile,[ValidateSet('combat','spawn')][string]$ProfileWindow='combat')
# Launches the CombatLoad benchmark Player at 1920x1080 exclusive fullscreen (D3D11, VSync off, no frame cap)
# and samples process/GPU telemetry every 5 s. It never sends input to the desktop; the Player feeds its own
# virtual Input System devices. The window covers the screen for about 6 minutes (15 cycles).
$ErrorActionPreference='Stop'
$project=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$exe=Join-Path $project "Tools/IronRequiem/Builds/CombatLoad/$Build/IronRequiemCombatLoad.exe"
$out=Join-Path $project "Tools/IronRequiem/Reports/CombatLoad/$Run"
if(!(Test-Path -LiteralPath $exe)){throw "Build not found: $Build"}
$arguments=@('-force-d3d11','-screen-width','1920','-screen-height','1080','-screen-fullscreen','1','-window-mode','exclusive','--iron-output',$out,'--iron-cycles',"$Cycles",'--iron-profile',$(if($Profile){'1'}else{'0'}),'--iron-profile-window',$ProfileWindow,'-logFile',"$out-player.log")
$game=Start-Process -FilePath $exe -ArgumentList $arguments -WorkingDirectory $project -PassThru
[pscustomobject]@{pid=$game.Id;utc=[DateTime]::UtcNow.ToString('o');build=$Build;run=$Run;cycles=$Cycles;arguments=$arguments;binarySHA256=(Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash;powerScheme=(powercfg /getactivescheme | Out-String).Trim();battery=(Get-CimInstance Win32_Battery | Select-Object BatteryStatus,EstimatedChargeRemaining)} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath "$out-launch.json"
$telemetry=New-Object System.Collections.Generic.List[object]
$timer=[Diagnostics.Stopwatch]::StartNew()
while(!$game.HasExited)
{
    $game.Refresh()
    $telemetry.Add([pscustomobject]@{seconds=$timer.Elapsed.TotalSeconds;workingSetBytes=$game.WorkingSet64;cpuSeconds=$game.TotalProcessorTime.TotalSeconds;gpu=(nvidia-smi --query-gpu=memory.used,utilization.gpu,temperature.gpu,power.draw,pstate,clocks.current.graphics --format=csv,noheader,nounits | Out-String).Trim()})
    if($timer.Elapsed.TotalSeconds -gt 150+$Cycles*24){$game.CloseMainWindow() | Out-Null;throw 'Benchmark exceeded timeout; requested graceful close.'}
    Start-Sleep -Seconds 5
}
$telemetry | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath "$out-system.json"
[pscustomobject]@{run=$Run;exitCode=$game.ExitCode;wallSeconds=$timer.Elapsed.TotalSeconds;completed=(Test-Path -LiteralPath "$out.done");failed=(Test-Path -LiteralPath "$out.failed");metrics=(Test-Path -LiteralPath "$out.json")} | ConvertTo-Json
