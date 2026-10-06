$paths = @(
    "C:\Program Files\Git\bin\git.exe",
    "C:\Program Files\Git\cmd\git.exe",
    "C:\Program Files (x86)\Git\bin\git.exe",
    "C:\Program Files (x86)\Git\cmd\git.exe",
    "C:\Users\win 10\AppData\Local\Programs\Git\bin\git.exe",
    "C:\Users\win 10\AppData\Local\Programs\Git\cmd\git.exe",
    "C:\ProgramData\chocolatey\bin\git.exe",
    "C:\tools\git\bin\git.exe"
)

foreach ($p in $paths) {
    if (Test-Path $p) {
        Write-Host "FOUND: $p"
        & $p --version
        exit 0
    }
}

Write-Host "Not found in standard paths, checking PATH env..."
$env:PATH.Split(";") | ForEach-Object {
    if (Test-Path "$_\git.exe") {
        Write-Host "FOUND IN PATH: $_\git.exe"
    }
}
