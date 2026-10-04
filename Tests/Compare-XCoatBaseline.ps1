param(
    [Parameter(Mandatory=$true)][string]$BaselineDirectory,
    [Parameter(Mandatory=$true)][string]$CandidateDirectory
)
$ErrorActionPreference='Stop'
$files=@(Get-ChildItem -LiteralPath $BaselineDirectory -File -Filter '*.png')
if($files.Count -eq 0){throw 'No baseline PNGs'}
$comparisons=@(foreach($file in $files) {
    $target=Join-Path $CandidateDirectory $file.Name
    if(-not (Test-Path -LiteralPath $target)){throw "Missing $target"}
    $a=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    $b=(Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
    [pscustomobject]@{Name=$file.Name;ReferenceSHA256=$a;CandidateSHA256=$b;Identical=($a -ceq $b)}
})
$report=[pscustomobject]@{Baseline=(Resolve-Path -LiteralPath $BaselineDirectory).Path;Candidate=(Resolve-Path -LiteralPath $CandidateDirectory).Path;Images=$files.Count;Changed=@($comparisons | Where-Object {-not $_.Identical}).Count;Comparisons=$comparisons}
$report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $CandidateDirectory 'coat-baseline-comparison.json') -Encoding UTF8
if($report.Changed){throw "$($report.Changed) legacy images changed"}
Write-Output "PASS: $($report.Images) PNGs byte-identical to pre-clearcoat baseline."
