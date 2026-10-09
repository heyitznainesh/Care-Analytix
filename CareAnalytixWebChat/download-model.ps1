# Download Phi-4-mini ONNX (CPU-int4) Model
$ErrorActionPreference = "Stop"

$modelDir = Join-Path -Path $PSScriptRoot -ChildPath "Phi4Model"
if (!(Test-Path -Path $modelDir)) {
    New-Item -ItemType Directory -Force -Path $modelDir | Out-Null
}

$baseUrl = "https://huggingface.co/microsoft/Phi-4-mini-instruct-onnx/resolve/main/cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4/"

$files = @(
    "added_tokens.json",
    "config.json",
    "configuration_phi3.py",
    "genai_config.json",
    "phi4-mini-instruct-cpu-int4-rtn-block-32-acc-level-4.onnx",
    "phi4-mini-instruct-cpu-int4-rtn-block-32-acc-level-4.onnx.data",
    "special_tokens_map.json",
    "tokenizer.json",
    "tokenizer_config.json"
)

Write-Host "Downloading Microsoft Phi-4-mini ONNX model (approx 2GB). This may take several minutes..."

foreach ($file in $files) {
    $url = $baseUrl + $file
    $dest = Join-Path -Path $modelDir -ChildPath $file
    if (!(Test-Path -Path $dest)) {
        Write-Host "Downloading $file..."
        Invoke-WebRequest -Uri $url -OutFile $dest
    } else {
        Write-Host "File $file already exists, skipping."
    }
}

Write-Host "Model downloaded successfully to $modelDir!"
