using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Tinkwell.Cli.Commands.Certs;

public static class SelfSignedCertificate
{
    public record CreateOptions(string CommonName, int ValidityYears, string Password);

    public static X509Certificate2 Create(CreateOptions options)
    {
        using var rsa = RSA.Create(2048);
        var subject = new X500DistinguishedName($"CN={options.CommonName}");

        var request = new CertificateRequest(
            subject,
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(
            certificateAuthority: false,
            hasPathLengthConstraint: false,
            pathLengthConstraint: 0,
            critical: true));

        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
            critical: false));

        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(
            request.PublicKey,
            critical: false));

        var now = DateTimeOffset.UtcNow;
        var rawCert = request.CreateSelfSigned(now.AddDays(-1), now.AddYears(options.ValidityYears));

        var pfxBytes = rawCert.Export(X509ContentType.Pfx, options.Password);
        return X509CertificateLoader.LoadPkcs12(pfxBytes, options.Password,
            X509KeyStorageFlags.DefaultKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
    }

    public static void Export(X509Certificate2 cert, string baseFileName, bool exportPem, out string[] exportedFiles)
    {
        string pfxFilePath = baseFileName + ".pfx";
        string certPemFilePath = baseFileName + "-cert.pem";
        string keyPemFilePath = baseFileName + "-key.pem";

        File.WriteAllBytes(pfxFilePath, cert.Export(X509ContentType.Pfx));

        if (exportPem)
        {
            var pemCert = ExportToPem(cert.RawData, "CERTIFICATE");
            var pemKey = ExportToPem(cert.GetRSAPrivateKey()?.ExportPkcs8PrivateKey()!, "PRIVATE KEY");

            File.WriteAllText(certPemFilePath, pemCert);
            File.WriteAllText(keyPemFilePath, pemKey);

            exportedFiles = [pfxFilePath, certPemFilePath, keyPemFilePath];
        }
        else
        {
            exportedFiles = [pfxFilePath];

        }

        static string ExportToPem(byte[] bytes, string label) =>
            $"-----BEGIN {label}-----\n{Convert.ToBase64String(bytes, Base64FormattingOptions.InsertLineBreaks)}\n-----END {label}-----\n";
    }
}