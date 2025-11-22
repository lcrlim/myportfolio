# GameServer 자동 빌드 스크립트
# 사용법: .\build.ps1 [Release|Debug] [rebuild]

param(
    [string]$BuildType = "Release",
    [string]$Action = "build"
)

$ErrorActionPreference = "Stop"

# 색상 출력 함수
function Write-Info {
    param([string]$Message)
    Write-Host "[INFO] $Message" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "[SUCCESS] $Message" -ForegroundColor Green
}

function Write-Error-Custom {
    param([string]$Message)
    Write-Host "[ERROR] $Message" -ForegroundColor Red
}

function Write-Warning-Custom {
    param([string]$Message)
    Write-Host "[WARNING] $Message" -ForegroundColor Yellow
}

# vcpkg 경로 찾기
$VcpkgPaths = @(
    "D:\work\dev\vcpkg\scripts\buildsystems\vcpkg.cmake"
    #"C:\vcpkg\scripts\buildsystems\vcpkg.cmake",
    #"$env:USERPROFILE\vcpkg\scripts\buildsystems\vcpkg.cmake"
)

$VcpkgToolchain = $null
foreach ($path in $VcpkgPaths) {
    if (Test-Path $path) {
        $VcpkgToolchain = $path
        break
    }
}

if (-not $VcpkgToolchain) {
    Write-Error-Custom "vcpkg toolchain file not found!"
    Write-Warning-Custom "Please install vcpkg or update the path in this script."
    exit 1
}

Write-Info "Using vcpkg toolchain: $VcpkgToolchain"

# 프로젝트 루트 확인
if (-not (Test-Path "CMakeLists.txt")) {
    Write-Error-Custom "CMakeLists.txt not found in current directory!"
    Write-Warning-Custom "Please run this script from the project root."
    exit 1
}

# 필수 파일 확인
$RequiredFiles = @("game.proto", "Common.h", "GameServer.cpp", "TestClient.cpp")
foreach ($file in $RequiredFiles) {
    if (-not (Test-Path $file)) {
        Write-Error-Custom "Required file not found: $file"
        exit 1
    }
}

Write-Success "All required files found."

# Rebuild 옵션 처리
if ($Action -eq "rebuild") {
    Write-Warning-Custom "Rebuilding from scratch..."
    if (Test-Path "build") {
        Write-Info "Removing existing build directory..."
        Remove-Item -Path "build" -Recurse -Force
    }
}

# build 디렉토리 생성
if (-not (Test-Path "build")) {
    Write-Info "Creating build directory..."
    New-Item -ItemType Directory -Path "build" | Out-Null
}

Set-Location "build"

# CMake 설정
Write-Info "Running CMake configuration..."
Write-Info "Build Type: $BuildType"

try {
    $cmakeOutput = cmake .. `
        -DCMAKE_TOOLCHAIN_FILE="$VcpkgToolchain" `
        -A x64 `
        -DCMAKE_BUILD_TYPE=$BuildType `
        2>&1
    
    Write-Host $cmakeOutput
    
    if ($LASTEXITCODE -ne 0) {
        throw "CMake configuration failed!"
    }
    
    Write-Success "CMake configuration completed."
} catch {
    Write-Error-Custom $_.Exception.Message
    Set-Location ..
    exit 1
}

# Proto 파일 생성 확인
Write-Info "Checking proto file generation..."
$generatedPath = "generated"
if (Test-Path $generatedPath) {
    Write-Success "Generated directory exists: $generatedPath"
} else {
    Write-Warning-Custom "Generated directory not yet created (will be created during build)"
}

# 빌드 실행
Write-Info "Building project..."
Write-Info "This may take a few minutes..."

try {
    $buildOutput = cmake --build . --config $BuildType -- /m 2>&1
    Write-Host $buildOutput
    
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed!"
    }
    
    Write-Success "Build completed successfully!"
} catch {
    Write-Error-Custom $_.Exception.Message
    Set-Location ..
    exit 1
}

# 생성 파일 확인
Write-Info "Verifying build output..."

$expectedFiles = @(
    "generated\game.pb.h",
    "generated\game.pb.cc",
    "generated\game.grpc.pb.h",
    "generated\game.grpc.pb.cc",
    "$BuildType\game_server.exe",
    "$BuildType\test_client.exe"
)

$allFilesExist = $true
foreach ($file in $expectedFiles) {
    if (Test-Path $file) {
        Write-Success "✓ $file"
    } else {
        Write-Error-Custom "✗ $file (missing)"
        $allFilesExist = $false
    }
}

Set-Location ..

if ($allFilesExist) {
    Write-Host ""
    Write-Success "=========================================="
    Write-Success "Build completed successfully!"
    Write-Success "=========================================="
    Write-Host ""
    Write-Info "To run the server:"
    Write-Host "  cd build\$BuildType" -ForegroundColor Yellow
    Write-Host "  .\game_server.exe" -ForegroundColor Yellow
    Write-Host ""
    Write-Info "To run the test client (in another terminal):"
    Write-Host "  cd build\$BuildType" -ForegroundColor Yellow
    Write-Host "  .\test_client.exe localhost:50051 single" -ForegroundColor Yellow
    Write-Host "  .\test_client.exe localhost:50051 load 1000 60" -ForegroundColor Yellow
    Write-Host ""
} else {
    Write-Error-Custom "Some files are missing. Build may have failed."
    exit 1
}

exit 0