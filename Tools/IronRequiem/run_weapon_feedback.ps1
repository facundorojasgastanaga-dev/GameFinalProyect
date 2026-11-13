param([Parameter(Mandatory=$true)][ValidateSet('Apply','Edit','Audit','Editor')][string]$Mode)
$ErrorActionPreference='Stop'
$chargeRoot='C:/Users/gehbd/ProyectoGameFinal'
Set-Location -LiteralPath $chargeRoot
if(Get-Process Unity -ErrorAction SilentlyContinue){throw 'Exit the existing Unity Editor first; a second Editor will not be started.'}
$chargeLock=$null
try {
 if(Test-Path Temp/UnityLockfile){$chargeLock=[IO.File]::Open((Join-Path $chargeRoot 'Temp/UnityLockfile'),[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)}
} finally {if($chargeLock){$chargeLock.Dispose()}}
$chargeArguments=@('-batchmode','-projectPath',$chargeRoot,'-logFile',"$chargeRoot/Tools/IronRequiem/Reports/weapon-feedback-$($Mode.ToLower()).log")
if($Mode -eq 'Editor'){
 $chargeArguments=@('-projectPath',$chargeRoot,'-executeMethod','IronRequiem.Editor.CharacterVerificationRunner.Run','-logFile',"$chargeRoot/Tools/IronRequiem/Reports/weapon-feedback-editor.log")
}
elseif($Mode -eq 'Apply'){$chargeArguments+=@('-executeMethod','IronRequiem.Editor.WeaponFeedbackUpgrade.Apply','-quit')}
elseif($Mode -eq 'Audit'){$chargeArguments+=@('-executeMethod','IronRequiem.Editor.IronRequiemSetupTools.WriteAudit','-quit')}
else {
 $chargePlatform='EditMode'
 $chargeAssembly='IronRequiem.EditModeTests'
 $chargeArguments+=@('-runTests','-testPlatform',$chargePlatform,'-assemblyNames',$chargeAssembly,'-testResults',"$chargeRoot/Tools/IronRequiem/Reports/weapon-feedback-$($Mode.ToLower()).xml")
}
$chargeProcess=Start-Process -FilePath 'C:/Program Files/Unity/Hub/Editor/6000.5.5f1/Editor/Unity.exe' -ArgumentList $chargeArguments -WindowStyle Hidden -PassThru
if($Mode -eq 'Editor'){Write-Output "Interactive Unity verification started, PID: $($chargeProcess.Id)";exit 0}
$chargeProcess.WaitForExit()
Write-Output "Unity $Mode exit code: $($chargeProcess.ExitCode)"
exit $chargeProcess.ExitCode
