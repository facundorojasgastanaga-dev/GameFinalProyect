param([string]$ProjectRoot='C:/Users/gehbd/ProyectoGameFinal')
$ErrorActionPreference='Stop'
Set-Location -LiteralPath $ProjectRoot
$backup='Tools/IronRequiem/Backups/20261029-mechanics-v02'
$baseline=Get-Content -LiteralPath "$backup/baseline.json" -Raw|ConvertFrom-Json
$allowed=@(
 'Assets/FPS/Scripts/Gameplay/PlayerCharacterController.cs',
 'Assets/FPS/Scripts/Gameplay/Managers/PlayerWeaponsManager.cs',
 'Assets/FPS/Scripts/Game/Shared/WeaponController.cs',
 'Assets/FPS/Scripts/AI/EnemyController.cs',
 'Assets/FPS/Scripts/UI/WeaponHUDManager.cs',
 'Assets/FPS/InputSystem_Actions.inputactions',
 'Assets/IronRequiem/Scripts/Enemies/DetachableCannon.cs',
 'Assets/IronRequiem/Scripts/IronRequiem.Runtime.asmdef',
 'Assets/IronRequiem/Editor/IronRequiem.Editor.asmdef',
 'Assets/IronRequiem/Editor/CharacterVerificationRunner.cs',
 'Assets/IronRequiem/Tests/EditMode/IronRequiem.EditModeTests.asmdef',
 'Assets/IronRequiem/Tests/PlayMode/IronRequiem.PlayModeTests.asmdef',
 'Assets/IronRequiem/Prefabs/Player/Player_IR_v01.prefab',
 'Assets/IronRequiem/Prefabs/Enemies/T07_IR_v01.prefab',
 'Assets/IronRequiem/Scenes/CharacterLab.unity'
)
$differences=@(foreach($entry in $baseline){
 $path=$entry.path.Replace('\','/')
 if(!(Test-Path -LiteralPath $entry.path)){[pscustomobject]@{path=$path;state='missing';expected=$false}}
 elseif((Get-FileHash -LiteralPath $entry.path -Algorithm SHA256).Hash -ne $entry.sha256){[pscustomobject]@{path=$path;state='changed';expected=$allowed -contains $path}}
})
$unexpected=@($differences|Where-Object{!$_.expected})
$tests=@(foreach($mode in @('play','edit')){
 $path="Tools/IronRequiem/Reports/character-$mode-visible.xml"
 [xml]$xml=Get-Content -LiteralPath $path
 $run=$xml.'test-run';$cases=@($xml.SelectNodes('//test-case'))
 [pscustomobject]@{mode=$mode;result=$run.result;total=[int]$run.total;passed=[int]$run.passed;failed=[int]$run.failed;originalPassed=@($cases|Where-Object{$_.fullname -match 'Character(Behavior|Asset)Tests\.' -and $_.result -eq 'Passed'}).Count;report=$path}
})
$before=Get-Content -LiteralPath "$backup/Assets/FPS/InputSystem_Actions.inputactions" -Raw|ConvertFrom-Json
$after=Get-Content -LiteralPath Assets/FPS/InputSystem_Actions.inputactions -Raw|ConvertFrom-Json
$oldInputPreserved=$true
foreach($map in $before.maps){
 $current=$after.maps|Where-Object id -EQ $map.id
 if(!$current){$oldInputPreserved=$false;continue}
 foreach($action in $map.actions){$match=$current.actions|Where-Object id -EQ $action.id;if(($action|ConvertTo-Json -Compress -Depth 10) -ne ($match|ConvertTo-Json -Compress -Depth 10)){$oldInputPreserved=$false}}
 foreach($binding in $map.bindings){$match=$current.bindings|Where-Object id -EQ $binding.id;if(($binding|ConvertTo-Json -Compress -Depth 10) -ne ($match|ConvertTo-Json -Compress -Depth 10)){$oldInputPreserved=$false}}
}
$missingMeta=@(Get-ChildItem -LiteralPath Assets/IronRequiem -File -Recurse|Where-Object{$_.Extension -ne '.meta' -and !(Test-Path -LiteralPath ($_.FullName+'.meta'))}|ForEach-Object FullName)
$audit=Get-Content -LiteralPath Tools/IronRequiem/Reports/unity-audit.json -Raw|ConvertFrom-Json
$result=[pscustomobject]@{
 utc=[DateTime]::UtcNow.ToString('O');baselineFiles=$baseline.Count;changes=$differences;unexpectedChanges=$unexpected;
 originalInputActionsAndBindingsPreserved=$oldInputPreserved;tests=$tests;missingMetas=$missingMeta;
 compilationErrors=$audit.compilationErrors;playing=$audit.playing;
 allChecksPass=($unexpected.Count -eq 0 -and $oldInputPreserved -and $missingMeta.Count -eq 0 -and $audit.compilationErrors.Count -eq 0 -and ($tests|Where-Object{$_.failed -eq 0 -and $_.passed -eq $_.total -and $_.originalPassed -eq 3}).Count -eq 2 -and ($tests|Where-Object mode -EQ play).total -ge 12 -and ($tests|Where-Object mode -EQ edit).total -ge 5)
}
$result|ConvertTo-Json -Depth 9|Set-Content -LiteralPath Tools/IronRequiem/Reports/mechanics-verification.json
$result|ConvertTo-Json -Depth 9
if(!$result.allChecksPass){exit 1}
