# Setup

## Certificates

We need HTPPS for gRPC servers and, because of the potentially distributed nature of the system, we cannot rely on the auto-generated development certificate. We need one where `SA` is not `localhost` but your machine name.

You can put this certificate where you want but, needless to say, it MUST NOT be included in source control. We use `build/config/certs/` (which is already included in `.gitignore`).

You have to create two environment variables: `TINKWELL_CERT_PATH` (containing the path - absolute or relative - to certificate) and `TINKWELL_CERT_PASS` containing the password. If you're using VS you can easily set them in the Debug Properties for `Tinkwell.Supervisor`.

## Generate the Certificate

There are two small helper scripts in `Development/`, use `create-dev-cert.sh` in Linux or `Create-DevCert.ps1` in a PowerShell terminal in Windows.

In Linux you are also going to need to manually install it as trusted root certificate, the procedure is slightly different for different distributions.

In Windows the script will do it for you (after asking confirmation).