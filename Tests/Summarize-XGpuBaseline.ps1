param([Parameter(Mandatory=$true)][string]$ReportPath)
$ErrorActionPreference = 'Stop'
$culture = [System.Globalization.CultureInfo]::InvariantCulture
$path = (Resolve-Path -LiteralPath $ReportPath).Path
$report = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
if ($report.status -ne 'complete') { throw "Incomplete benchmark: $($report.status) $($report.error)" }
function Median($values) {
    $v = @($values | Sort-Object)
    if (-not $v.Count) { throw 'Empty sample set' }
    if ($v.Count % 2) { return [double]$v[[int][Math]::Floor($v.Count / 2)] }
    return ([double]$v[$v.Count / 2 - 1] + [double]$v[$v.Count / 2]) / 2
}
function F([double]$n) { $n.ToString('F4', $culture) }
$rows = foreach ($case in $report.cases) {
    if ($case.samples.Count -ne $report.samplesPerBlock * $report.rounds) { throw "Missing samples: $($case.id)" }
    $values = @($case.samples.gpuMs | Sort-Object)
    foreach ($v in $values) { if ([double]::IsNaN($v) -or [double]::IsInfinity($v) -or $v -lt 0) { throw "Invalid time: $($case.id)" } }
    $roundMedians = @(for ($r = 0; $r -lt $report.rounds; $r++) { Median @($case.samples | Where-Object round -eq $r | ForEach-Object gpuMs) })
    [pscustomobject]@{
        id=$case.id; preset=$case.preset; layout=$case.layout; eyePairs=$case.eyePairs; lights=$case.lights
        addDisabled=$case.addDisabled; gpuMedianMs=(Median $values)
        gpuP10Ms=$values[[int][Math]::Floor(($values.Count - 1) * .1)]
        gpuP90Ms=$values[[int][Math]::Floor(($values.Count - 1) * .9)]
        roundMinMedianMs=($roundMedians | Measure-Object -Minimum).Minimum
        roundMaxMedianMs=($roundMedians | Measure-Object -Maximum).Maximum
        cpuRenderCallMedianMs=(Median $case.samples.renderCallMs)
        cpuFenceReadbackMedianMs=(Median $case.samples.fenceReadbackMs)
        psInvocations=$case.psInvocations; primitives=$case.primitives; samples=$values.Count
        shaderPath=$case.shaderPath; dependencyHash=$case.dependencyHash
    }
}
$lookup = @{}; foreach ($row in $rows) { $lookup[$row.id] = $row }
if ($report.vplusSetup -and $report.stencilSetup) {
    foreach ($row in $rows | Where-Object { $_.preset -like 'v-eye*' -and $_.lights -gt 1 }) {
        $base = $lookup["$($row.preset)-$($row.layout)-p$($row.eyePairs)-l1"]
        if (-not $base -or $row.psInvocations -le $base.psInvocations) {
            throw "V+ eye Add produced no extra pixel invocations: $($row.id)"
        }
    }
}
foreach ($row in $rows | Where-Object addDisabled) {
    $baseId = "$($row.preset)-$($row.layout)-p$($row.eyePairs)-l1"
    if ($row.primitives -ne $lookup[$baseId].primitives -or $row.psInvocations -ne $lookup[$baseId].psInvocations) {
        throw "Disabling ForwardAdd did not reproduce Base GPU work: $($row.id)"
    }
}
foreach ($preset in @('x-eye-off','x-eye-coat','x-eye-flat','x-eye-bowl','x-eye-map','x-eye-map-coat','x-eye-map-disp','x-eye-map-disp-coat')) {
    foreach ($layout in @('close','crowd')) {
        $pairs = if ($layout -eq 'close') { @(1) } else { @(1,4,8) }
        foreach ($p in $pairs) {
            $base = $lookup["$preset-$layout-p$p-l1"]
            foreach ($l in @(4,8)) {
                $additional = $lookup["$preset-$layout-p$p-l$l"]
                if ($additional.primitives * 2 -ne $base.primitives * ($l + 1)) { throw "Unexpected X pass execution count: $($additional.id)" }
            }
        }
    }
}
$out = Split-Path -Parent $path
$rows | Export-Csv -LiteralPath (Join-Path $out 'summary.csv') -NoTypeInformation -Encoding UTF8
$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('# GPU Baseline Measurements')
$lines.Add('')
$lines.Add("GPU: $($report.gpu). Unity $($report.unity), $($report.api), $($report.colorSpace).")
$lines.Add("UTC interval: $($report.startedUtc) to $($report.finishedUtc).")
$lines.Add("Cases: $($rows.Count). Valid timed samples: $(($rows.samples | Measure-Object -Sum).Sum). Three randomized blocks per case; median, not FPS-derived timing.")
$lines.Add("Mesh subdivisions: $(if ($report.meshSubdivisions) { $report.meshSubdivisions } else { 128 }). Triangles per eye: $(if ($report.trianglesPerEye) { $report.trianglesPerEye } else { 32768 }).")
$lines.Add('')
$lines.Add($report.method)
$lines.Add('')
$lines.Add($report.scope)
$lines.Add('')
$lines.Add($(if ($report.stencilSetup) { $report.stencilSetup } else { 'WARNING: unseeded pilot. V+ Eye Add was stencil-rejected in this fixture; do not use its multi-light times for a working-Add comparison.' }))
$lines.Add('')
$lines.Add($report.comparison)
$lines.Add('')
$lines.Add($(if ($report.vplusSetup) { $report.vplusSetup } else { 'WARNING: V+ optional ForwardAdd was at its default off value in this pilot. Do not interpret multi-light costs as active V+ additional lighting.' }))
$lines.Add('')
$lines.Add('GPU intervals include camera draw scheduling/pipeline work, not just fragment instructions. CPU Camera.Render wall-time can stall; it is not pure CPU work. The raw report retains every sample and its block/order. Round-median spread is a stability diagnostic, not an independent-sample confidence interval. No clock locking or driver power-policy changes were made.')
$lines.Add('')
foreach ($layout in @('close','crowd')) {
    foreach ($pairs in $(if ($layout -eq 'close') { @(1) } else { @(1,4,8) })) {
        $lines.Add("## $layout / $pairs Eye Pair(s)")
        $lines.Add('')
        $lines.Add('| Preset | 1 light GPU ms | 4 lights GPU ms | 8 lights GPU ms |')
        $lines.Add('| --- | ---: | ---: | ---: |')
        foreach ($preset in @($rows | Where-Object { $_.layout -eq $layout -and $_.eyePairs -eq $pairs -and -not $_.addDisabled } | Select-Object -ExpandProperty preset -Unique)) {
            $cells = foreach ($l in @(1,4,8)) { $r = $lookup["$preset-$layout-p$pairs-l$l"]; if ($r) { F $r.gpuMedianMs } else { 'n/a' } }
            $lines.Add("| $preset | $($cells -join ' | ') |")
        }
        $lines.Add('')
    }
}
$lines.Add('## Matched Feature Deltas')
$lines.Add('')
$lines.Add('| Layout / pairs / lights | Coat only minus off | Flat minus off | Bowl minus off | Map minus off | Dispersion increment over map | Coat increment over map+dispersion |')
$lines.Add('| --- | ---: | ---: | ---: | ---: | ---: | ---: |')
foreach ($layout in @('close','crowd')) {
    foreach ($pairs in $(if ($layout -eq 'close') { @(1) } else { @(1,4,8) })) {
        foreach ($l in @(1,4,8)) {
            $suffix = "-$layout-p$pairs-l$l"
            $b = $lookup["x-eye-off$suffix"].gpuMedianMs
            $map = $lookup["x-eye-map$suffix"].gpuMedianMs
            $disp = $lookup["x-eye-map-disp$suffix"].gpuMedianMs
            $d = @(
                (F ($lookup["x-eye-coat$suffix"].gpuMedianMs - $b)),
                (F ($lookup["x-eye-flat$suffix"].gpuMedianMs - $b)),
                (F ($lookup["x-eye-bowl$suffix"].gpuMedianMs - $b)),
                (F ($map - $b)), (F ($disp - $map)),
                (F ($lookup["x-eye-map-disp-coat$suffix"].gpuMedianMs - $disp))
            )
            $lines.Add("| $layout / $pairs / $l | $($d -join ' | ') |")
        }
    }
}
$lines.Add('')
$lines.Add('## ForwardAdd Diagnostic')
$lines.Add('')
$lines.Add('Same eight physical lights, per-material ForwardAdd disabled only in diagnostic copies. Pipeline counters must match the corresponding one-light Base work. These copies are not proposed rendering modes: disabling Add also removes its lighting.')
$lines.Add('')
$lines.Add('| Case | All passes ms | Add disabled ms | Difference ms | Full / no-Add PS invocations |')
$lines.Add('| --- | ---: | ---: | ---: | ---: |')
foreach ($r in $rows | Where-Object addDisabled) {
    $full = $lookup[$r.id.Replace('-noadd','')]
    $lines.Add("| $($r.id) | $(F $full.gpuMedianMs) | $(F $r.gpuMedianMs) | $(F ($full.gpuMedianMs - $r.gpuMedianMs)) | $($full.psInvocations) / $($r.psInvocations) |")
}
$lines.Add('')
$lines.Add("Empty-camera median GPU interval: $(F $lookup['empty-crowd-p0-l1'].gpuMedianMs) ms. A zero at this resolution does not prove zero overhead. Tables report raw interval times; no arbitrary subtraction/clamping of noise.")
$lines.Add('')
$lines.Add('See summary.csv for p10/p90, min/max round medians, CPU-call/readback times and shader dependency hashes. Materials.txt records effective material inputs, passes and keywords. PNGs use the same display transform and are outside timed samples.')
[System.IO.File]::WriteAllLines((Join-Path $out 'summary.md'), $lines)
Write-Output "PASS: $($rows.Count) cases; all samples finite; ForwardAdd execution counters match. Reports: $out"
