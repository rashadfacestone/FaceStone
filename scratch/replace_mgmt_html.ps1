$indexHtmlPath = "F:\FaceStoneWebsite\wwwroot\index.html"
$newMarkupPath = "F:\FaceStoneWebsite\scratch\new_management_markup.html"

$lines = [System.IO.File]::ReadAllLines($indexHtmlPath, [System.Text.Encoding]::UTF8)

$startIndex = -1
$endIndex = -1

for ($i = 0; $i -lt $lines.Length; $i++) {
    if ($lines[$i] -match '<div class="management-page-root" id="management-page-root"') {
        $startIndex = $i
    }
    if ($lines[$i] -match '</div> <!-- End management-page-root -->') {
        $endIndex = $i
        break
    }
}

Write-Host "Found start: $startIndex (line $($startIndex + 1)), end: $endIndex (line $($endIndex + 1))"

if ($startIndex -ge 0 -and $endIndex -gt $startIndex) {
    $newMarkup = [System.IO.File]::ReadAllText($newMarkupPath, [System.Text.Encoding]::UTF8)
    
    $prefix = $lines[0..($startIndex - 1)]
    $suffix = $lines[($endIndex + 1)..($lines.Length - 1)]
    
    $result = [System.Collections.Generic.List[string]]::new()
    $result.AddRange($prefix)
    $result.Add($newMarkup)
    $result.AddRange($suffix)
    
    [System.IO.File]::WriteAllLines($indexHtmlPath, $result, [System.Text.Encoding]::UTF8)
    Write-Host "Successfully replaced management section in $indexHtmlPath"
} else {
    Write-Error "Could not locate management-page-root boundaries!"
}
