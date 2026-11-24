[CmdletBinding()]
param([int]$Port = 19091, [int]$DurationSeconds = 0)

$ErrorActionPreference = "Stop"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$outputPath = Join-Path $repositoryRoot "artifacts/monitoring/alerts.jsonl"
New-Item -ItemType Directory -Force -Path (Split-Path $outputPath) | Out-Null
$listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Any, $Port)
$listener.Start()
$deadline = if ($DurationSeconds -gt 0) { [DateTimeOffset]::UtcNow.AddSeconds($DurationSeconds) } else { $null }
try {
    while ($null -eq $deadline -or [DateTimeOffset]::UtcNow -lt $deadline) {
        if (-not $listener.Pending()) { Start-Sleep -Milliseconds 100; continue }
        $client = $listener.AcceptTcpClient()
        $stream = $client.GetStream()
        $reader = [IO.StreamReader]::new($stream, [Text.Encoding]::ASCII, $false, 8192, $true)
        $writer = [IO.StreamWriter]::new($stream, [Text.Encoding]::ASCII, 8192, $true)
        try {
            $headers = [Collections.Generic.List[string]]::new()
            while (($line = $reader.ReadLine()) -ne $null -and $line.Length -gt 0) { $headers.Add($line) }
            $length = 0
            $contentHeader = $headers | Where-Object { $_ -match '^Content-Length:\s*(\d+)' } | Select-Object -First 1
            if ($contentHeader -and $contentHeader -match '(\d+)') { $length = [int]$Matches[1] }
            $body = if ($length -gt 0) { $buffer = [char[]]::new($length); [void]$reader.ReadBlock($buffer, 0, $length); -join $buffer } else { "{}" }
            $payload = $body | ConvertFrom-Json
            $summary = [ordered]@{
                receivedAtUtc = [DateTimeOffset]::UtcNow
                status = [string]$payload.status
                alerts = @($payload.alerts | ForEach-Object { [ordered]@{ alertname = $_.labels.alertname; severity = $_.labels.severity; job = $_.labels.job } })
            }
            Add-Content -LiteralPath $outputPath -Value ($summary | ConvertTo-Json -Compress -Depth 6)
            $response = "HTTP/1.1 200 OK`r`nContent-Length: 0`r`nConnection: close`r`n`r`n"
            $writer.Write($response); $writer.Flush()
        }
        finally {
            $writer.Dispose(); $reader.Dispose(); $stream.Dispose(); $client.Dispose()
        }
    }
}
finally { $listener.Stop() }
