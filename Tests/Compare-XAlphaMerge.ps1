param(
    [Parameter(Mandatory=$true)][string]$BaselineDirectory,
    [Parameter(Mandatory=$true)][string]$MergedDirectory
)
$ErrorActionPreference = 'Stop'
$baseline = (Resolve-Path -LiteralPath $BaselineDirectory).Path
$merged = (Resolve-Path -LiteralPath $MergedDirectory).Path
function Compare-Image([string]$Reference, [string]$Candidate, [string]$Group) {
    if (-not (Test-Path -LiteralPath $Candidate -PathType Leaf)) { throw "Missing candidate: $Candidate" }
    $a = (Get-FileHash -LiteralPath $Reference -Algorithm SHA256).Hash
    $b = (Get-FileHash -LiteralPath $Candidate -Algorithm SHA256).Hash
    if ($a -cne $b) { throw "Image changed: $Candidate (reference: $Reference)" }
    [pscustomobject]@{
        Group = $Group
        Reference = [IO.Path]::GetFileName($Reference)
        Candidate = [IO.Path]::GetFileName($Candidate)
        SHA256 = $a
        Identical = $true
    }
}
$originalImages = @(Get-ChildItem -LiteralPath $baseline -Filter '*.png' -File)
$prepassImages = @(Get-ChildItem -LiteralPath $merged -Filter 'MainAlphaXPrepass-*.png' -File)
if ($originalImages.Count -eq 0 -or $prepassImages.Count -eq 0) { throw 'Missing baseline or merged-prepass captures' }
$comparisons = @(
    foreach ($file in $originalImages) {
        Compare-Image $file.FullName (Join-Path $merged $file.Name) 'Existing variants'
    }
    foreach ($file in $prepassImages) {
        $oldName = $file.Name.Replace('MainAlphaXPrepass-', 'MainAlphaX2Pass-')
        Compare-Image (Join-Path $merged $oldName) $file.FullName 'Merged vs legacy prepass'
    }
)
[pscustomobject]@{
    BaselineDirectory = $baseline
    MergedDirectory = $merged
    ExistingImages = $originalImages.Count
    PrepassPairs = $prepassImages.Count
    AllIdentical = $true
    Comparisons = $comparisons
}
