param([string]$Root = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
function Assert($condition, [string]$message) { if (-not $condition) { throw $message } }
[xml]$manifest = Get-Content -LiteralPath (Join-Path $Root 'manifest.xml') -Raw
[xml]$tips = Get-Content -LiteralPath (Join-Path $Root 'Tooltips/tom_x_tooltips.xml') -Raw
$total = 0
$shared = @{}
foreach ($entry in $manifest.SelectNodes('//MaterialEditor/Shader')) {
    $name = $entry.Name.Substring(4)
    $source = Get-Content -LiteralPath (Join-Path $Root "Shaders/Tom/$name.shader") -Raw
    $header = $source.Substring(0, $source.IndexOf('SubShader'))
    $properties = @([regex]::Matches($header, '(?m)^\s*(?:\[[^\]]+\]\s*)*_(\w+)\s*\("[^"]*",\s*(2D|Color|Vector|Float|Range\(([^)]*)\))\)'))
    $catalog = $tips.SelectSingleNode("//Shader[@Name='$($entry.Name)']")
    Assert ($properties.Count -eq $entry.Property.Count) "$name property count differs"
    foreach ($collection in @($entry, $catalog)) {
        Assert (@($collection.Property | Group-Object Name | Where-Object Count -gt 1).Count -eq 0) "$name duplicate property"
        Assert ($collection.Property.Count -eq $properties.Count) "$name tooltip/property count differs"
    }
    foreach ($p in $properties) {
        $key = $p.Groups[1].Value
        $node = $entry.SelectSingleNode("Property[@Name='$key']")
        $tip = $catalog.SelectSingleNode("Property[@Name='$key']")
        Assert ($null -ne $node -and $null -ne $tip -and $tip.InnerText.Length -gt 12) "$name/$key missing metadata"
        $expected = switch ($p.Groups[2].Value) { '2D' {'Texture'} 'Color' {'Color'} 'Vector' {'Color'} default {'Float'} }
        Assert ($node.Type -ceq $expected) "$name/$key type mismatch"
        if ($p.Groups[3].Success) {
            $actual = @($node.Range -split ',' | ForEach-Object { [double]::Parse($_.Trim(), [cultureinfo]::InvariantCulture) })
            $wanted = @($p.Groups[3].Value -split ',' | ForEach-Object { [double]::Parse($_.Trim(), [cultureinfo]::InvariantCulture) })
            Assert ($actual.Count -eq 2 -and $actual[0] -eq $wanted[0] -and $actual[1] -eq $wanted[1]) "$name/$key range mismatch"
        }
        Assert (@($node.Attributes | Where-Object Name -match 'Default').Count -eq 0) "$name/$key must preserve saved material values"
        Assert ($null -ne $catalog.SelectSingleNode("Category[@Name='$($node.Category)']")) "$name/$key category lacks help: $($node.Category)"
        # Game aliases and per-family debug/coverage differences are intentionally not homogenized.
        if ($node.Category -in @('Lighting','Material','Direct Specular','Environment Reflection','MatCap','Rim','Clearcoat')) {
            $schema = $node.Category+'|'+$node.Type+'|'+($node.Range -replace '\s','')
            if ($shared.ContainsKey($key)) { Assert ($schema -ceq $shared[$key]) "$name/$key shared control differs" }
            else { $shared[$key]=$schema }
        }
        $total++
    }
}
Assert ($tips.SelectSingleNode('//Shader[@Name="tom/EyeX"]/Property[@Name="MainTex"]').InnerText -match 'alpha blended') 'EyeX coverage help regressed'
Assert ($tips.SelectSingleNode('//Shader[@Name="tom/SkinX"]/Property[@Name="Cutoff"]').InnerText -match 'fixed 0.5') 'Skin cutoff help regressed'
Assert ($tips.SelectSingleNode('//Shader[@Name="tom/SkinX"]/Property[@Name="OcclusionMap"]').InnerText -match 'white is unoccluded') 'Skin AO channel help lost'
Write-Output "PASS: eight shaders, $total controls: exact types/ranges, help/category coverage, no forced defaults and shared ME schema consistency."
