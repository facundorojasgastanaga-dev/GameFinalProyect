param([string]$ProjectRoot='C:/Users/gehbd/ProyectoGameFinal')
$ErrorActionPreference='Stop'
Set-Location -LiteralPath $ProjectRoot
$baseline=Get-Content 'Tools/IronRequiem/Backups/20261029-weapon-feedback/baseline.json' -Raw|ConvertFrom-Json
$allowed=@(
 'Assets/IronRequiem/Scripts/Weapons/ChargedProjectileDamage.cs',
 'Assets/IronRequiem/Editor/DesignV02Upgrade.cs',
 'Assets/IronRequiem/Tests/EditMode/MechanicsAssetTests.cs',
 'Assets/IronRequiem/Tests/PlayMode/MechanicsBehaviorTests.cs',
 'Assets/IronRequiem/Prefabs/Weapons/Weapon_IR_Pistol.prefab',
 'Assets/IronRequiem/Prefabs/Weapons/Weapon_T07_Temporary.prefab',
 'Assets/IronRequiem/Prefabs/Weapons/Projectile_T07_Charged.prefab'
)
$changes=@(foreach($entry in $baseline){
 if(!(Test-Path -LiteralPath $entry.path)){[pscustomobject]@{path=$entry.path;expected=$false;state='missing'}}
 elseif((Get-FileHash -LiteralPath $entry.path -Algorithm SHA256).Hash -ne $entry.sha256){[pscustomobject]@{path=$entry.path;expected=($allowed -contains $entry.path);state='changed'}}
})
$unexpected=@($changes|Where-Object{!$_.expected})
$tests=@(foreach($mode in @('play','edit')){
 $path="Tools/IronRequiem/Reports/weapon-feedback-$mode.xml"
 [xml]$xml=Get-Content -LiteralPath $path
 $run=$xml.'test-run';$cases=@($xml.SelectNodes('//test-case'))
 [pscustomobject]@{mode=$mode;result=$run.result;total=[int]$run.total;passed=[int]$run.passed;failed=[int]$run.failed;originalPassed=@($cases|Where-Object{$_.fullname -match 'Character(Behavior|Asset)Tests\.' -and $_.result -eq 'Passed'}).Count;report=$path}
})
$missingMeta=@(Get-ChildItem Assets/IronRequiem -Recurse -File|Where-Object{$_.Extension -ne '.meta' -and !(Test-Path -LiteralPath ($_.FullName+'.meta'))}|ForEach-Object FullName)
$logs=@(Get-Content Tools/IronRequiem/Reports/weapon-feedback-apply.log,Tools/IronRequiem/Reports/weapon-feedback-editor.log)
$errors=@($logs|Where-Object{$_ -match 'error CS\d+|Scripts have compiler errors|executeMethod.*threw an exception'})
$audit=Get-Content Tools/IronRequiem/Reports/unity-audit.json -Raw|ConvertFrom-Json
$result=[pscustomobject]@{
 utc=[DateTime]::UtcNow.ToString('O');baselineFiles=$baseline.Count;changes=$changes;unexpectedChanges=$unexpected;
 tests=$tests;missingMetas=$missingMeta;compilationErrors=$errors;auditUtc=$audit.utc;playing=$audit.playing;scenes=$audit.scenes;
 environment='Interactive Unity Editor with virtual Input System devices';
 applyShutdownIssue='Native SerializationCache/UnloadDomain crash after prefabs were saved. Retained in weapon-feedback-apply.log; interactive tests subsequently passed.';
 allChecksPass=($unexpected.Count -eq 0 -and $missingMeta.Count -eq 0 -and $errors.Count -eq 0 -and !$audit.playing -and
   @($audit.scenes|Where-Object{$_.dirty -or $_.missingScripts -ne 0}).Count -eq 0 -and $audit.compilationErrors.Count -eq 0 -and
   @($tests|Where-Object{$_.failed -eq 0 -and $_.passed -eq $_.total -and $_.originalPassed -eq 3}).Count -eq 2 -and
   ($tests|Where-Object mode -EQ play).total -ge 21 -and ($tests|Where-Object mode -EQ edit).total -ge 7)
}
$result|ConvertTo-Json -Depth 8|Set-Content Tools/IronRequiem/Reports/weapon-feedback-verification.json
$result|ConvertTo-Json -Depth 8
if(!$result.allChecksPass){exit 1}
