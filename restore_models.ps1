$baseDir = $PSScriptRoot
$resourcesDir = Join-Path $baseDir "Backend\Bff.Service\resources"
$rawDataDir = Join-Path $resourcesDir "RawData"
$outputFile = Join-Path $resourcesDir "ggml-base.en.bin"

if (Test-Path $outputFile) {
    Write-Host "Model file already exists: $outputFile"
    # Optional: Verify hash or force overwrite logic here
    # exit 0 
}

Write-Host "Restoring model from chunks..."

if (!(Test-Path $rawDataDir)) {
    Write-Error "RawData directory not found!"
    exit 1
}

$chunks = Get-ChildItem -Path $rawDataDir -Filter "ggml-base.en.bin.part*" | Sort-Object Name

if ($chunks.Count -eq 0) {
    Write-Error "No model chunks found in $rawDataDir"
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

