param([string]$ProjectRoot = 'C:/Users/gehbd/ProyectoGameFinal')
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $ProjectRoot
$baseline = Get-Content -LiteralPath Tools/IronRequiem/Backups/20261029-characters/baseline-hashes.json -Raw | ConvertFrom-Json
$differences = @(foreach ($entry in $baseline) {
    if (!(Test-Path -LiteralPath $entry.path)) { [pscustomobject]@{ path=$entry.path; state='missing' } }
    elseif ((Get-FileHash -LiteralPath $entry.path -Algorithm SHA256).Hash -ne $entry.sha256) { [pscustomobject]@{ path=$entry.path; state='changed' } }
})
$previous = Get-Content -LiteralPath Tools/IronRequiem/Backups/20261028-111809/baseline.json -Raw | ConvertFrom-Json
$originalAssets = @($previous | Where-Object { $_.path.StartsWith('Assets/') })
$assetDifferences = @(foreach ($entry in $originalAssets) {
    if (!(Test-Path -LiteralPath $entry.path) -or (Get-FileHash -LiteralPath $entry.path -Algorithm SHA256).Hash -ne $entry.sha256) { $entry.path }
})
$sources = @(
    @{path='C:/Users/gehbd/OneDrive/Escritorio/Juegos GJ/vistasROBOT/T-07.fbx'; sha256='1DCC48CDF250FB66F609167ED5D20722754CA44302E85301A5F0802785FD05D3'},
    @{path='C:/Users/gehbd/OneDrive/Escritorio/Juegos GJ/vistasROBOT/T-07(Texture).glb'; sha256='27E4FAF73A2838FABC9528A1E56AA516D7D24761FAD33DA2C51F66CAF1F53BB0'}
)
$sourceResults = @(foreach ($source in $sources) { [pscustomobject]@{ path=$source.path; unchanged=((Get-FileHash -LiteralPath $source.path -Algorithm SHA256).Hash -eq $source.sha256) } })
$tests = @(foreach ($mode in @('edit','play')) {
    $path = "Tools/IronRequiem/Reports/character-$mode-visible.xml"
    if (Test-Path -LiteralPath $path) {
        [xml]$xml = Get-Content -LiteralPath $path
        $run = $xml.'test-run'
        [pscustomobject]@{ mode=$mode; result=$run.result; total=[int]$run.total; passed=[int]$run.passed; failed=[int]$run.failed; report=$path }
    } else { [pscustomobject]@{mode=$mode;result='not_run';total=0;passed=0;failed=0;report=$path} }
})
$missingMetas = @(Get-ChildItem -LiteralPath Assets/IronRequiem -File -Recurse | Where-Object { $_.Extension -ne '.meta' -and !(Test-Path -LiteralPath ($_.FullName+'.meta')) } | ForEach-Object { [IO.Path]::GetRelativePath($ProjectRoot,$_.FullName) })
$audit = Get-Content -LiteralPath Tools/IronRequiem/Reports/unity-audit.json -Raw | ConvertFrom-Json
$result = [pscustomobject]@{
    utc=[DateTime]::UtcNow.ToString('O'); protectedFiles=$baseline.Count; differences=$differences;
    originalAssets=$originalAssets.Count; originalAssetDifferences=$assetDifferences; sources=$sourceResults;
    tests=$tests; missingMetas=$missingMetas; unity=$audit.unity; pipeline=$audit.pipeline;
    compilationErrors=$audit.compilationErrors;
    allChecksPass=($differences.Count -eq 0 -and $assetDifferences.Count -eq 0 -and !($sourceResults | Where-Object { !$_.unchanged }) -and ($tests | Where-Object { $_.failed -eq 0 -and $_.passed -eq $_.total -and $_.total -gt 0 }).Count -eq 2 -and $missingMetas.Count -eq 0 -and $audit.compilationErrors.Count -eq 0)
}
$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath Tools/IronRequiem/Reports/character-verification.json
$result | ConvertTo-Json -Depth 8
if (!$result.allChecksPass) { exit 1 }
