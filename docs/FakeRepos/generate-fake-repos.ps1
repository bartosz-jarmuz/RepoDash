param(
  [Parameter(Mandatory=$true)][string]$Root,
  [int]$Services = 6,
  [int]$Utilities = 4,
  [int]$Contracts = 5
)

$ErrorActionPreference = "Stop"

function New-Repo([string]$Path, [string]$Name, [bool]$WithSln) {
  $repoPath = Join-Path $Path $Name
  New-Item -ItemType Directory -Path $repoPath -Force | Out-Null
  New-Item -ItemType Directory -Path (Join-Path $repoPath ".git") -Force | Out-Null
  if ($WithSln) { New-Item -ItemType File -Path (Join-Path $repoPath "$Name.sln") -Force | Out-Null }
}

New-Item -ItemType Directory -Path $Root -Force | Out-Null

$ws = Join-Path $Root "web-services"
$ut = Join-Path $Root "utilities"
$ct = Join-Path $Root "contracts"

New-Item -ItemType Directory -Path $ws,$ut,$ct -Force | Out-Null

1..$Services | ForEach-Object { New-Repo $ws "Service$_" $true }
1..$Utilities | ForEach-Object { New-Repo $ut "Tool$_" $true }
1..$Contracts | ForEach-Object { New-Repo $ct "Contracts.$_" $true }

Write-Host "Fake repos created under $Root"