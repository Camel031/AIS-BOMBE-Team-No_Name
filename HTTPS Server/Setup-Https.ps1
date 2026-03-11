# 1. Point domain to localhost in hosts file
$hostsPath = "$env:SystemRoot\System32\drivers\etc\hosts"
Add-Content -Path $hostsPath -Value "`n127.0.0.1 submit.bombe.top"
Write-Host "[+] Added entry to hosts file."

# 2. Generate Self-Signed Certificate
# We store it in the "My" (Personal) store first
$cert = New-SelfSignedCertificate -DnsName "submit.bombe.top" -CertStoreLocation "cert:\LocalMachine\My"
Write-Host "[+] Certificate generated: $($cert.Thumbprint)"

# 3. Trust the Certificate
# Import into "Trusted Root Certification Authorities" so the Zig client accepts it
$rootStore = New-Object System.Security.Cryptography.X509Certificates.X509Store "Root", "LocalMachine"
$rootStore.Open("ReadWrite")
$rootStore.Add($cert)
$rootStore.Close()
Write-Host "[+] Certificate added to Trusted Root Store."

# 4. Bind Certificate to Port 443 using netsh
$guid = [Guid]::NewGuid().ToString("B")
$thumbprint = $cert.Thumbprint

# Clear any existing binding on 443 just in case
netsh http delete sslcert ipport=0.0.0.0:443 | Out-Null

# Add the new binding
Write-Host "[*] Binding certificate to port 443..."
netsh http add sslcert ipport=0.0.0.0:443 certhash=$thumbprint appid=$guid

Write-Host "`n[SUCCESS] Environment setup complete."
