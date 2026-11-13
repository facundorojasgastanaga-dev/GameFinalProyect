param(
    [string]$Source = "$PSScriptRoot/ManualEdits/20261106_SawBotSession/IR_Combat01_Loop_Manual.mmp",
    [string]$Output = "$PSScriptRoot/ManualEdits/20261106_SawBotSession/IR_Combat01_Rock_Bar09.mmp"
)
$ErrorActionPreference = 'Stop'
if (Test-Path -LiteralPath $Output) { throw "Output already exists: $Output" }
[xml]$project = Get-Content -LiteralPath $Source -Raw
$container = $project.'lmms-project'.song.trackcontainer
$originalTracks = @($container.track)

function Set-Attr($element, [string]$key, $value) {
    $element.SetAttribute($key, [string]$value)
}
function New-Note($pattern, [int]$position, [int]$key, [int]$length, [int]$velocity, [int]$pan = 0) {
    $note = $project.CreateElement('note')
    foreach ($pair in @(@('pos',$position),@('key',$key),@('len',$length),@('vol',$velocity),@('pan',$pan))) {
        Set-Attr $note $pair[0] $pair[1]
    }
    [void]$pattern.AppendChild($note)
}
function Copy-Layer([string]$sourceName, [string]$name, [int]$volume, [int]$pan, [int]$patch) {
    $layer = ($originalTracks | Where-Object name -eq $sourceName).CloneNode($true)
    Set-Attr $layer 'name' $name
    Set-Attr $layer.instrumenttrack 'vol' $volume
    Set-Attr $layer.instrumenttrack 'pan' $pan
    Set-Attr $layer.instrumenttrack.instrument.sf2player 'patch' $patch
    Set-Attr $layer.instrumenttrack.instrument.sf2player 'gain' '1'
    Set-Attr $layer.instrumenttrack.instrument.sf2player 'reverbOn' '0'
    foreach ($pattern in @($layer.pattern)) {
        if ([int]$pattern.pos -lt 1536) { [void]$layer.RemoveChild($pattern); continue }
        Set-Attr $pattern 'name' "$name $(if ([int]$pattern.pos -eq 1536) {'Breakdown'} else {'Final'})"
    }
    [void]$container.AppendChild($layer)
    return $layer
}

# Two complementary electric takes enter at bar 9, with no notes before it.
$left = Copy-Layer 'Guitar L' 'ROCK - Rhythm L - bar 9' 78 -52 30
$right = Copy-Layer 'Guitar R' 'ROCK - Rhythm R - bar 9' 74 52 30
foreach ($layer in @($left,$right)) {
    foreach ($shape in $layer.SelectNodes('.//waveshapercontrols')) {
        Set-Attr $shape 'inputGain' '2.4'
        Set-Attr $shape 'outputGain' '0.48'
    }
    foreach ($pattern in @($layer.pattern)) {
        foreach ($note in @($pattern.note)) {
            # Longer chord tails contrast with the original palm-muted layers.
            if ([int]$note.len -ge 20) { Set-Attr $note 'len' ([Math]::Min(42, [int]$note.len + 10)) }
            if ($layer -eq $right) { Set-Attr $note 'pos' ([int]$note.pos + 2) }
        }
    }
}

# Electric guitar follows the written melody one octave below the synthesizer.
$lead = Copy-Layer 'Lead' 'ROCK - Electric melody - bar 9' 92 8 30
$guitarEffects = ($originalTracks | Where-Object name -eq 'Guitar L').instrumenttrack.fxchain.CloneNode($true)
[void]$lead.instrumenttrack.ReplaceChild($guitarEffects, $lead.instrumenttrack.fxchain)
foreach ($pattern in @($lead.pattern)) {
    foreach ($note in @($pattern.note)) {
        Set-Attr $note 'key' ([int]$note.key - 12)
        Set-Attr $note 'len' ([Math]::Max(3, [int]$note.len - 2))
        Set-Attr $note 'vol' 94
    }
}

# Separate editable tom/crash layer complements existing kick/snare, without doubling them.
$drums = Copy-Layer 'Drums' 'ROCK - Toms and cymbals - bar 9' 88 0 0
foreach ($pattern in @($drums.pattern)) {
    foreach ($note in @($pattern.note)) { [void]$pattern.RemoveChild($note) }
    for ($bar = 0; $bar -lt 8; $bar++) {
        $start = $bar * 192
        New-Note $pattern $start 29 8 96 -18 # GM low floor tom 41 (LMMS key = MIDI - 12).
        New-Note $pattern ($start + 72) 33 8 80 15
        New-Note $pattern ($start + 120) 29 8 88 -18
        if ($bar % 2 -eq 0) { New-Note $pattern $start 37 30 90 24 }
        if ($bar % 4 -eq 3) {
            $keys = @(38,38,35,35,33,33,29,29)
            for ($hit = 0; $hit -lt 8; $hit++) {
                New-Note $pattern ($start + 96 + $hit * 12) $keys[$hit] 7 (78 + $hit * 3) (-30 + $hit * 8)
            }
        }
    }
}

$settings = [System.Xml.XmlWriterSettings]::new()
# Leave output headroom for the additional instruments; original track levels stay intact.
Set-Attr $project.'lmms-project'.head 'mastervol' '49'
$settings.Indent = $true
$settings.Encoding = [System.Text.UTF8Encoding]::new($false)
$writer = [System.Xml.XmlWriter]::Create($Output, $settings)
try { $project.Save($writer) } finally { $writer.Dispose() }

# Check the original music and loop length survived unchanged.
[xml]$saved = Get-Content -LiteralPath $Output -Raw
$savedTracks = @($saved.'lmms-project'.song.trackcontainer.track)
for ($index = 0; $index -lt $originalTracks.Count; $index++) {
    if ($originalTracks[$index].OuterXml -ne $savedTracks[$index].OuterXml) { throw "Original track changed: $index" }
}
foreach ($layer in $savedTracks | Select-Object -Skip $originalTracks.Count) {
    foreach ($pattern in @($layer.pattern)) {
        if ([int]$pattern.pos -lt 1536) { throw 'New layer starts before bar 9' }
        foreach ($note in @($pattern.note)) {
            if ([int]$pattern.pos + [int]$note.pos + [int]$note.len -gt 4608) { throw 'Note exceeds loop' }
        }
    }
}
Write-Output "Created $Output. Original tracks unchanged. Four new editable layers from bar 9 (10.667 seconds), 180 BPM, 24 bars."
