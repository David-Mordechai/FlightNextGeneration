$baseDir = $PSScriptRoot
# Target is the parent directory (resources)
$outputFile = Join-Path $baseDir "..\ggml-base.en.bin"
$outputFile = [System.IO.Path]::GetFullPath($outputFile)

if (Test-Path $outputFile) {
    Write-Host "Model file already exists: $outputFile"
    # exit 0 
}

Write-Host "Restoring model from chunks in $baseDir..."

$chunks = Get-ChildItem -Path $baseDir -Filter "ggml-base.en.bin.part*" | Sort-Object Name

if ($chunks.Count -eq 0) {
    Write-Error "No model chunks found in $baseDir"
    exit 1
}

$fsOut = [System.IO.File]::Create($outputFile)
try {
    foreach ($chunk in $chunks) {
        Write-Host "Merging $($chunk.Name)..."
        $fsChunk = [System.IO.File]::OpenRead($chunk.FullName)
        try {
            $fsChunk.CopyTo($fsOut)
        }
        finally {
            $fsChunk.Close()
        }
    }
}
finally {
    $fsOut.Close()
}

Write-Host "Model restored successfully: $outputFile"