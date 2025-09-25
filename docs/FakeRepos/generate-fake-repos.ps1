param(
    [Parameter(Mandatory = $true)]
    [string]$RootPath
)

$structure = @{
    "web-services" = @("CheckoutService", "CatalogService", "InventoryGateway", "CustomerProfile")
    "utilities"     = @("LoggingFramework", "PerformanceMonitor", "JobScheduler")
    "contracts"     = @("Contracts.Ordering", "Contracts.Payments", "Contracts.Inventory")
    "data"          = @("AnalyticsWarehouse", "ReportingHub")
}

Write-Host "Creating fake repository tree under $RootPath"

foreach ($segment in $structure.GetEnumerator()) {
    foreach ($repo in $segment.Value) {
        $repoPath = Join-Path (Join-Path $RootPath $segment.Key) $repo
        New-Item -ItemType Directory -Force -Path $repoPath | Out-Null
        New-Item -ItemType Directory -Force -Path (Join-Path $repoPath ".git") | Out-Null
        $slnPath = Join-Path $repoPath "$repo.sln"
        Set-Content -Path $slnPath -Value "Microsoft Visual Studio Solution File, Format Version 12.00" -Encoding ASCII
    }
}

# Add a SQL repo without solution file.
$sqlRepo = Join-Path (Join-Path $RootPath "data") "DataWarehouseScripts"
New-Item -ItemType Directory -Force -Path $sqlRepo | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $sqlRepo ".git") | Out-Null

Write-Host "Fake repositories created successfully."
