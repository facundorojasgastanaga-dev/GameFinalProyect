param(
    [string]$Source = "$PSScriptRoot/ManualEdits/20261106_SawBotSession/IR_Combat01_Rock_Bar09.mmp",
    [string]$Output = "$PSScriptRoot/ManualEdits/20261106_SawBotSession/IR_Combat01_Rock_Drop_v02.mmp"
)
$ErrorActionPreference = 'Stop'
if (Test-Path -LiteralPath $Output) { throw "Output already exists: $Output" }
[xml]$project = Get-Content -LiteralPath $Source -Raw
$tracks = @($project.'lmms-project'.song.trackcontainer.track)
function Set-Attr($element, [string]$key, $value) { $element.SetAttribute($key, [string]$value) }
function Add-Note($pattern, [int]$position, [int]$key, [int]$length, [int]$velocity, [int]$pan = 0) {
    $note = $project.CreateElement('note')
    foreach ($pair in @(@('pos',$position),@('key',$key),@('len',$length),@('vol',$velocity),@('pan',$pan))) {
        Set-Attr $note $pair[0] $pair[1]
    }
    [void]$pattern.AppendChild($note)
}

$originalGuitar = ($tracks | Where-Object name -eq 'Guitar L').CloneNode($true)
foreach ($track in $tracks) {
    # A tiny breath before the downbeat makes the entrance distinct, not merely louder.
    foreach ($pattern in @($track.pattern | Where-Object { [int]$_.pos -eq 0 })) {
        foreach ($note in @($pattern.note)) {
            $position = [int]$note.pos
            if ($position -ge 1524) { [void]$pattern.RemoveChild($note) }
            elseif ($position + [int]$note.len -gt 1524) { Set-Attr $note 'len' (1524 - $position) }
        }
    }
    if ($track.name -in @('Guitar L','Guitar R','Guitar Low')) {
        foreach ($pattern in @($track.pattern | Where-Object { [int]$_.pos -ge 1536 })) {
            foreach ($note in @($pattern.note)) { Set-Attr $note 'vol' ([int]([int]$note.vol * 0.58)) }
        }
    }
}

for ($side = 0; $side -lt 2; $side++) {
    $track = $tracks | Where-Object name -eq @('ROCK - Rhythm L - bar 9','ROCK - Rhythm R - bar 9')[$side]
    Set-Attr $track 'name' @('DROP - Heavy guitar L','DROP - Heavy guitar R')[$side]
    Set-Attr $track.instrumenttrack 'vol' @(136,130)[$side]
    Set-Attr $track.instrumenttrack 'pan' @(-62,62)[$side]
    foreach ($shape in $track.SelectNodes('.//waveshapercontrols')) {
        Set-Attr $shape 'inputGain' '2.8'
        Set-Attr $shape 'outputGain' '0.52'
    }
    foreach ($pattern in @($track.pattern)) {
        $reference = @($originalGuitar.pattern | Where-Object pos -eq $pattern.pos)[0]
        foreach ($note in @($pattern.note)) { [void]$pattern.RemoveChild($note) }
        Set-Attr $pattern 'name' "HEAVY RIFF $(if ([int]$pattern.pos -eq 1536) {'ENTRY'} else {'DRIVE'})"
        for ($bar = 0; $bar -lt 8; $bar++) {
            $barStart = $bar * 192
            $rhythm = if ($bar -eq 0 -and [int]$pattern.pos -eq 1536) {
                @(@(0,56,100),@(72,8,94),@(84,8,90),@(96,20,100),@(120,8,94),@(132,8,91),@(144,20,100),@(168,8,95),@(180,7,91))
            } elseif ($bar % 2 -eq 0) {
                @(@(0,20,100),@(24,8,91),@(36,8,88),@(48,20,98),@(84,8,92),@(96,20,100),@(120,8,94),@(132,8,90),@(144,20,99),@(168,8,92),@(180,7,90))
            } else {
                @(@(0,20,100),@(24,8,93),@(36,8,90),@(48,20,98),@(72,8,93),@(84,8,89),@(96,20,100),@(120,8,95),@(132,8,91),@(144,8,96),@(156,8,93),@(168,8,96),@(180,7,99))
            }
            foreach ($hit in $rhythm) {
                $time = $barStart + [int]$hit[0]
                $lastTime = ($reference.note | Where-Object { [int]$_.pos -le $time } | ForEach-Object { [int]$_.pos } | Measure-Object -Maximum).Maximum
                $root = ($reference.note | Where-Object { [int]$_.pos -eq $lastTime } | ForEach-Object { [int]$_.key } | Measure-Object -Minimum).Minimum
                foreach ($interval in @(0,7,12)) {
                    Add-Note $pattern ($time + $side * 2) ($root + $interval) $hit[1] ($hit[2] - $side * 3)
                }
            }
        }
    }
}

$lead = $tracks | Where-Object name -eq 'ROCK - Electric melody - bar 9'
Set-Attr $lead 'name' 'DROP - Electric melody'
Set-Attr $lead.instrumenttrack 'vol' 112

# Revoice the existing kit: strong downbeat, readable backbeat and double-kick bursts.
# Replace hits rather than stack duplicate kicks/snares on the original drum part.
$drums = $tracks | Where-Object name -eq 'Drums'
foreach ($pattern in @($drums.pattern | Where-Object { [int]$_.pos -ge 1536 })) {
    foreach ($note in @($pattern.note)) { [void]$pattern.RemoveChild($note) }
    for ($bar = 0; $bar -lt 8; $bar++) {
        $start = $bar * 192
        $kicks = @(0,24,36,72,96,120,132,168,180)
        if ([int]$pattern.pos -eq 1536 -and $bar -eq 0) { $kicks = @(0,72,84,96,120,132,144,168,180) }
        foreach ($kick in $kicks) { Add-Note $pattern ($start + $kick) 24 8 $(if ($kick % 48 -eq 0) {100} else {92}) }
        foreach ($snare in @(48,144)) { Add-Note $pattern ($start + $snare) 26 10 100 }
        for ($tick = 0; $tick -lt 192; $tick += 24) { Add-Note $pattern ($start + $tick) 30 6 $(if ($tick % 48 -eq 0) {78} else {66}) 24 }
        Add-Note $pattern $start $(if ($bar % 4 -eq 0) {37} else {40}) 24 98 -20
    }
}

$toms = $tracks | Where-Object name -eq 'ROCK - Toms and cymbals - bar 9'
Set-Attr $toms 'name' 'DROP - Thunder toms and fills'
Set-Attr $toms.instrumenttrack 'vol' 138
Set-Attr $toms.instrumenttrack.instrument.sf2player 'patch' 16
foreach ($pattern in @($toms.pattern)) {
    foreach ($note in @($pattern.note)) { [void]$pattern.RemoveChild($note) }
    for ($bar = 0; $bar -lt 8; $bar++) {
        $start = $bar * 192
        Add-Note $pattern $start 29 12 100 -24
        if ($bar % 2 -eq 0) {
            foreach ($hit in @(@(60,35,90),@(72,33,97),@(84,29,100))) { Add-Note $pattern ($start + $hit[0]) $hit[1] 9 $hit[2] 12 }
        } else {
            $keys = @(38,35,33,29)
            for ($hit = 0; $hit -lt 4; $hit++) { Add-Note $pattern ($start + 144 + $hit * 12) $keys[$hit] 9 (91 + $hit * 3) (30 - $hit * 20) }
        }
        if ($bar % 4 -eq 3) {
            foreach ($hit in @(@(96,38),@(108,38),@(120,35),@(132,33))) { Add-Note $pattern ($start + $hit[0]) $hit[1] 8 97 0 }
        }
    }
}
Set-Attr $project.'lmms-project'.head 'mastervol' 45
$project.Save([System.IO.Path]::GetFullPath($Output))
[xml]$check = Get-Content -LiteralPath $Output -Raw
foreach ($note in $check.SelectNodes('//pattern/note')) {
    if ([int]$note.ParentNode.pos + [int]$note.pos + [int]$note.len -gt 4608) { throw 'Note beyond loop boundary' }
}
Write-Output "Created drop v02: bar 9, 180 BPM, 32-second loop, native editable tracks."
