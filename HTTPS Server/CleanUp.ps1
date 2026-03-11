# 1. Remove SSL Binding
netsh http delete sslcert ipport=0.0.0.0:443
Write-Host "[-] SSL Binding removed."

# 2. Remove entry from Hosts file
# Note: This is a simple filter; verify your hosts file manually if needed.
$hostsPath = "$env:SystemRoot\System32\drivers\etc\hosts"
$content = Get-Content $hostsPath
$newContent = $content | Where-Object { $_ -notmatch "submit.bombe.top" }
Set-Content -Path $hostsPath -Value $newContent
Write-Host "[-] Hosts file entry removed."

# 3. (Optional) You may want to manually remove the cert from "Trusted Root Certification Authorities"
# via 'certlm.msc' if you want a completely clean state.

Write-Host "[+] Cleanup complete."
