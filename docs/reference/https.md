# Enabling HTTPS

By default Tinkwell uses plain HTTP/2 cleartext (H2C) for all gRPC communication.
This is fine for single-machine development but insufficient for production or multi-machine deployments.
This guide covers the three `TlsMode` values and how to set up certificates.

## TLS modes

| Mode | Scheme | Certificate required | Client validation |
|------|--------|---------------------|-------------------|
| `None` | `http` | No | N/A |
| `SelfSigned` | `https` | Yes (`.pfx`) | Skipped — clients accept any server cert |
| `Standard` | `https` | Yes (`.pfx`) | Full — cert must be trusted by the OS |

Set the mode in `appsettings.json` (or any configuration source) on each runner:

```json
{
  "Tls": {
    "Mode": "SelfSigned",
    "CertificatePath": "certs/tinkwell-dev.pfx"
  }
}
```

If the `.pfx` file is **password-protected**, set `CertificatePassword` in the same `Tls` section.
Prefer supplying the value through **environment variables** or a **secret store** (for example `dotnet user-secrets` in development, or your platform’s secret manager in production) rather than checking a plaintext password into source control.
Configuration binding treats empty strings as present, so unset the property or override it when the cert has no password.

The coordinator does not serve gRPC, so it does not need a certificate.
Only runner processes (which host Kestrel) require TLS configuration.

## Creating a self-signed certificate

### Using `dotnet dev-certs` (simplest for development)

```bash
dotnet dev-certs https --export-path certs/tinkwell-dev.pfx --no-password --trust
```

This creates a `.pfx` file and trusts it on the current machine.
Works on Windows and macOS out of the box; on Linux it only creates the file (trust must be done manually — see below).

### Using OpenSSL (cross-platform)

```bash
openssl req -x509 -newkey rsa:2048 -nodes \
  -keyout tinkwell-dev.key -out tinkwell-dev.crt \
  -days 365 -subj "/CN=localhost" \
  -addext "subjectAltName=DNS:localhost,IP:127.0.0.1"

openssl pkcs12 -export -out certs/tinkwell-dev.pfx \
  -inkey tinkwell-dev.key -in tinkwell-dev.crt -passout pass:
```

### Using PowerShell (Windows only)

```powershell
$cert = New-SelfSignedCertificate `
    -DnsName "localhost" `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -NotAfter (Get-Date).AddYears(1) `
    -KeyAlgorithm RSA -KeyLength 2048

Export-PfxCertificate -Cert $cert -FilePath certs\tinkwell-dev.pfx -NoPassword
```

## Trusting the certificate

When using `TlsMode.SelfSigned`, client-side validation is disabled (the `ServiceDiscovery` channel uses `RemoteCertificateValidationCallback = (_, _, _, _) => true`).
You can skip trusting the certificate entirely in this mode.

For `TlsMode.Standard`, the certificate must be trusted by the OS so that gRPC clients (and browsers, if applicable) accept it.

### Windows

Option A — via PowerShell (requires elevation):

```powershell
Import-Certificate -FilePath tinkwell-dev.crt `
    -CertStoreLocation Cert:\LocalMachine\Root
```

Option B — via the certificate UI:

1. Double-click `tinkwell-dev.crt`.
2. Click **Install Certificate** → **Local Machine** → **Place all certificates in the following store** → **Trusted Root Certification Authorities** → **Finish**.

### Linux (Ubuntu / Debian)

```bash
sudo cp tinkwell-dev.crt /usr/local/share/ca-certificates/tinkwell-dev.crt
sudo update-ca-certificates
```

On Fedora/RHEL:

```bash
sudo cp tinkwell-dev.crt /etc/pki/ca-trust/source/anchors/
sudo update-ca-trust
```

### macOS

```bash
sudo security add-trusted-cert -d -r trustRoot \
    -k /Library/Keychains/System.keychain tinkwell-dev.crt
```

Or open **Keychain Access**, drag the `.crt` file into the **System** keychain, double-click it, expand **Trust**, and set **When using this certificate** to **Always Trust**.

## Verifying

After configuring TLS, the coordinator log should show:

```
gRPC runner listening on 127.0.0.1:4900 (HTTPS/2), 1 service(s) registered
```

Service URLs in `tw services list` will use the `https://` scheme, and `tw services find store` will return a URL like `https://127.0.0.1:4900/tinkwell.store.StateStore`.
