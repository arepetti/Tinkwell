Write-Host "This script will create a Tinkwell development certificate for local HTTPS using your computer name." -ForegroundColor Blue

$computerName = $env:COMPUTERNAME
Write-Host "Creating and installing self-signed certificate..." -ForegroundColor DarkGray
$cert = New-SelfSignedCertificate `
  -DnsName $computerName, "localhost" `
  -CertStoreLocation "cert:\CurrentUser\My" `
  -FriendlyName "Tinkwell DevCert for $computerName" `
  -KeyExportPolicy Exportable `
  -NotAfter (Get-Date).AddYears(5)

# Apparently New-SelfSignedCertificate may fail (in circumstances unknown to me) when running non-elevated
# even if we're targeting cert:\CurrentUser\My
$store = New-Object System.Security.Cryptography.X509Certificates.X509Store "My", "CurrentUser"
$store.Open("ReadWrite")
$store.Add($cert)
$store.Close()
Write-Host "Created cert with thumbprint: $($cert.Thumbprint)" -ForegroundColor DarkGray

Write-Host ""
Write-Host "Please enter a password to protect the certificate files"
$password = Read-Host -AsSecureString -Prompt "Password"

Write-Host "Choose where to export the certificate (.pfx file). You may enter either a folder or a full file path"
$exportInput = Read-Host -Prompt "Export path"

if (Test-Path $exportInput -PathType Container) {
    $computerName = $env:COMPUTERNAME
    $defaultFileName = "$computerName-tinkwell-devcert.pfx"
    $pfxPath = Join-Path $exportInput $defaultFileName
} else {
    $pfxPath = $exportInput
}

Write-Host "Exporting certificate to file..." -ForegroundColor DarkGray
Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $password

Write-Host "Certificate successfully saved to:" -ForegroundColor Green
Write-Host $pfxPath -ForegroundColor Cyan

$answer = Read-Host "Do you want to install this certificate? (y/N)"
switch ($answer.ToUpper()) {
    "Y" {
        Write-Host "Installing certificate to the Root store..." -ForegroundColor DarkGray
        $store = New-Object System.Security.Cryptography.X509Certificates.X509Store "Root", "CurrentUser"
        $store.Open("ReadWrite")
        $store.Add($cert)
        $store.Close()
        Write-Host "Certificate installed successfully!" -ForegroundColor Green}
    default {
        Write-Host "To run Tinkwell you will need to manually install this certificate." -ForegroundColor Yellow
    }
}