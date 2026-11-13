param([ValidatePattern('^[a-z0-9-]+$')][string]$Build='baseline-development',[ValidatePattern('^[a-z0-9-]+$')][string]$Run='smoke',[ValidateRange(1,15)][int]$Cycles=15,[switch]$Profile,[int]$AttachPid=0)
$ErrorActionPreference='Stop'
$project120=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$exe120=Join-Path $project120 "Tools/IronRequiem/Builds/Performance120/$Build/IronRequiemBenchmark.exe"
$out120=Join-Path $project120 "Tools/IronRequiem/Reports/Performance120/$Run"
if(!(Test-Path -LiteralPath $exe120)){throw "Build not found: $Build"}
$args120=@('-force-d3d11','-screen-width','1920','-screen-height','1080','-screen-fullscreen','1','-window-mode','exclusive','--iron-output',$out120,'--iron-cycles',"$Cycles",'--iron-profile',$(if($Profile){'1'}else{'0'}),'-logFile',"$out120-player.log")
# This is the interactive game window being measured, not a hidden/headless helper.
if($AttachPid -gt 0){$game120=Get-Process -Id $AttachPid}else{$game120=Start-Process -FilePath $exe120 -ArgumentList $args120 -WorkingDirectory $project120 -PassThru}
[pscustomobject]@{pid=$game120.Id;utc=[DateTime]::UtcNow.ToString('o');build=$Build;run=$Run;cycles=$Cycles;arguments=$args120;binarySHA256=(Get-FileHash -LiteralPath $exe120 -Algorithm SHA256).Hash;powerScheme=(powercfg /getactivescheme | Out-String).Trim()} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath "$out120-launch.json"
$telemetry120=New-Object System.Collections.Generic.List[object]
$timer120=[Diagnostics.Stopwatch]::StartNew()
while(!$game120.HasExited)
{
    $game120.Refresh()
    $telemetry120.Add([pscustomobject]@{seconds=$timer120.Elapsed.TotalSeconds;workingSetBytes=$game120.WorkingSet64;privateBytes=$game120.PrivateMemorySize64;cpuSeconds=$game120.TotalProcessorTime.TotalSeconds;gpu=(nvidia-smi --query-gpu=memory.used,utilization.gpu,temperature.gpu,power.draw,pstate,clocks.current.graphics --format=csv,noheader,nounits | Out-String).Trim()})
    if($timer120.Elapsed.TotalSeconds -gt 120+$Cycles*24){$game120.CloseMainWindow() | Out-Null;throw 'Benchmark exceeded timeout; requested graceful close.'}
    Start-Sleep -Seconds 5
}
$telemetry120 | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath "$out120-system.json"
[pscustomobject]@{run=$Run;exitCode=$game120.ExitCode;wallSeconds=$timer120.Elapsed.TotalSeconds;completed=(Test-Path -LiteralPath "$out120.done");metrics=(Test-Path -LiteralPath "$out120.json")} | ConvertTo-Json
