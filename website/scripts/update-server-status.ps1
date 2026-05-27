# Queries Arma 2 OA server via Steam A2S_INFO and writes status.json for dayzmodclassic.com.
param(
    [string]$ServerHost = '127.0.0.1',
    [int]$Port = 2302,
    [int]$QueryPort = 0,
    [string]$OutPath = 'C:\sites\dayzmodclassic.com\status.json',
    [string]$FallbackVersion = 'v8.15.1'
)

if ($QueryPort -le 0) { $QueryPort = $Port + 1 }

$ErrorActionPreference = 'Stop'

function Read-CStr {
    param([byte[]]$Bytes, [ref]$Index)
    $start = $Index.Value
    while ($Index.Value -lt $Bytes.Length -and $Bytes[$Index.Value] -ne 0) {
        $Index.Value++
    }
    $s = [System.Text.Encoding]::UTF8.GetString($Bytes, $start, $Index.Value - $start)
    $Index.Value++
    return $s
}

function Parse-A2SInfo {
    param([byte[]]$response)
    if ($response.Length -lt 20) { return $null }
    if ($response[0] -ne 0xFF -or $response[1] -ne 0xFF) { return $null }
    if ($response[4] -ne 0x49) { return $null }

    $i = 6
    $hostname = Read-CStr $response ([ref]$i)
    $map = Read-CStr $response ([ref]$i)
    [void](Read-CStr $response ([ref]$i))
    [void](Read-CStr $response ([ref]$i))
    $i += 2
    if ($i + 1 -ge $response.Length) { return $null }

    return @{
        Online     = $true
        Players    = [int]$response[$i]
        MaxPlayers = [int]$response[$i + 1]
        Map        = $map
        Hostname   = $hostname
    }
}

function Query-Server {
    param([string]$TargetHost, [int]$TargetPort)

    $client = New-Object System.Net.Sockets.UdpClient
    $client.Client.ReceiveTimeout = 4000
    try {
        $ip = [System.Net.Dns]::GetHostAddresses($TargetHost) |
            Where-Object { $_.AddressFamily -eq 'InterNetwork' } |
            Select-Object -First 1
        if (-not $ip) { return $null }

        $endpoint = New-Object System.Net.IPEndPoint($ip, $TargetPort)
        $payload = [byte[]](0xFF, 0xFF, 0xFF, 0xFF, 0x54) +
            [System.Text.Encoding]::ASCII.GetBytes('Source Engine Query') + [byte]0x00

        [void]$client.Send($payload, $payload.Length, $endpoint)
        $remote = New-Object System.Net.IPEndPoint([System.Net.IPAddress]::Any, 0)
        $response = $client.Receive([ref]$remote)
        return Parse-A2SInfo $response
    }
    catch {
        return $null
    }
    finally {
        $client.Dispose()
    }
}

$existingVersion = $FallbackVersion
if (Test-Path $OutPath) {
    try {
        $prev = Get-Content $OutPath -Raw | ConvertFrom-Json
        if ($prev.version) { $existingVersion = $prev.version }
    }
    catch { }
}

$result = Query-Server -TargetHost $ServerHost -TargetPort $QueryPort

$status = [ordered]@{
    online     = $false
    players    = 0
    maxPlayers = 40
    map        = 'chernarus'
    version    = $existingVersion
    hostname   = 'DayZ Mod Classic'
    updatedAt  = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
}

if ($result) {
    $status.online = $true
    $status.players = $result.Players
    if ($result.MaxPlayers -gt 0) { $status.maxPlayers = $result.MaxPlayers }
    if ($result.Map) {
        $status.map = ($result.Map -replace '[^a-zA-Z0-9]', '').ToLower()
        if (-not $status.map) { $status.map = 'chernarus' }
    }
    if ($result.Hostname) { $status.hostname = $result.Hostname }
}

$dir = Split-Path $OutPath -Parent
if ($dir -and -not (Test-Path $dir)) {
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
}

$status | ConvertTo-Json | Set-Content -Path $OutPath -Encoding UTF8
