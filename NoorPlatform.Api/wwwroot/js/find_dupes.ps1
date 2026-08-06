$dir = 'c:\AI_noor\NoorPlatform_Fixed (3)\NoorPlatform_Fixed\NoorPlatform\NoorPlatform.Api\wwwroot\js'
$files = Get-ChildItem -Path $dir -Filter *.js
$funcMap = @{}

$regex = [regex]'(?m)(?:^|\s)(?:async\s+)?function\s+([a-zA-Z0-9_]+)\s*\(|(?:^|\s)(?:const|let|var)\s+([a-zA-Z0-9_]+)\s*=\s*(?:async\s*)?(?:function\s*\(|\([^)]*\)\s*=>|[a-zA-Z0-9_]+\s*=>)'

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    $matches = $regex.Matches($content)
    
    foreach ($match in $matches) {
        $name = $match.Groups[1].Value
        if ([string]::IsNullOrEmpty($name)) {
            $name = $match.Groups[2].Value
        }
        if (-not [string]::IsNullOrEmpty($name) -and $name -notin @('if', 'for', 'while', 'switch', 'catch')) {
            if (-not $funcMap.ContainsKey($name)) {
                $funcMap[$name] = @()
            }
            $funcMap[$name] += $file.Name
        }
    }
}

foreach ($key in $funcMap.Keys) {
    $uniqueFiles = $funcMap[$key] | Select-Object -Unique
    if ($uniqueFiles.Count -gt 1) {
        Write-Output "$key -> $( $uniqueFiles -join ', ' )"
    }
}
