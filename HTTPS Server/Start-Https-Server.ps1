# --- Configuration ---
$url = "https://submit.bombe.top:443/"
$targetPaths = @("/submitMalAns", "/submitEdrAns")

Write-Host "[-] Starting HTTPS Server at $url ..." -ForegroundColor Cyan
Write-Host "[-] Monitoring Paths: $($targetPaths -join ', ')" -ForegroundColor Cyan

# Create Listener
$listener = New-Object System.Net.HttpListener
$listener.Prefixes.Add($url)

try {
    $listener.Start()
} catch {
    Write-Error "[!] Failed to start listener. Ensure PowerShell is running as Admin and Port 443 is free."
    exit
}

Write-Host "[+] Server is listening. Waiting for payload... (Press Ctrl+C to stop)" -ForegroundColor Green

try {
    while ($listener.IsListening) {
        # Blocking call waiting for a connection
        $context = $listener.GetContext()
        $request = $context.Request
        $response = $context.Response

        # Log connection
        $remoteIP = $request.RemoteEndPoint.Address.ToString()
        $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
        $requestPath = $request.Url.AbsolutePath
        
        if ($targetPaths -contains $requestPath) {
            Write-Host "`n[$timestamp] Connection from $remoteIP -> HIT! ($requestPath)" -ForegroundColor Yellow

            if ($request.HasEntityBody) {
                # Read the JSON payload
                $reader = New-Object System.IO.StreamReader($request.InputStream, $request.ContentEncoding)
                $body = $reader.ReadToEnd()
                $reader.Close()

                Write-Host "[-] Payload Content:" -ForegroundColor White
                Write-Host $body -ForegroundColor Cyan
                Write-Host "---------------------------------------------------"
            }

            # Prepare 200 OK response
            $responseString = '{"status": "received", "message": "Flag captured"}'
            $buffer = [System.Text.Encoding]::UTF8.GetBytes($responseString)
            
            $response.ContentType = "application/json"
            $response.ContentLength64 = $buffer.Length
            $response.StatusCode = 200
            $response.OutputStream.Write($buffer, 0, $buffer.Length)
        } 
        else {
            # Handle unknown paths (404)
            Write-Host "`n[$timestamp] Connection from $remoteIP -> 404 Not Found ($requestPath)" -ForegroundColor DarkGray
            $response.StatusCode = 404
        }
        
        $response.Close()
    }
} catch {
    # Clean exit on interrupt
    Write-Host "`n[!] Server stopping..."
} finally {
    if ($listener.IsListening) {
        $listener.Stop()
    }
    $listener.Close()
}
