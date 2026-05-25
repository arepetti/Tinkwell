# Tinkwell.Package

> Part of the [Tinkwell](https://github.com/arepetti/Tinkwell) platform.
> This library can be used independently in any .NET application — no Tinkwell installation required.

Secure package format for Tinkwell.
A Tinkwell package is a zip file with a well-defined structure and an SHA-512 + ECDSA P-384 integrity chain.

## Package structure

```
/
├── package.tw              # Manifest (name, version, author, etc.)
├── content/                # All packaged files
│   └── ...
└── security/
    ├── signatures.tw       # SHA-512 hash + size of every file outside security/
    └── signature.sig       # ECDSA signature of SHA-512(signatures.tw)
```

## Manifest properties

The `package.tw` manifest supports the following known properties.
Any other key-value pair is preserved in the `Properties` dictionary and passed through to consumers.

| Key | Required | Description |
|-----|----------|-------------|
| `name` | Yes | Package identifier (block name) |
| `format-version` | No | Manifest format version (default: 1) |
| `type` | No | Content type (eg "plugin", default `null`) |
| `subtype` | No | Content subtype (default `null`) |
| `version` | No | Package version (semver) |
| `author` | No | Primary author name |
| `author-email` | No | Author contact email |
| `company` | No | Company or organization name |
| `company-website` | No | Company website URL |
| `company-email` | No | Company contact email |
| `support-email` | No | Support/help desk email |
| `description` | No | Short package description |
| `license` | No | License identifier (e.g. MIT, Apache-2.0) |
| `license-url` | No | URL to full license text |
| `copyright` | No | Copyright notice |
| `contributors` | No | Comma-separated contributor names |
| `project-website` | No | Project homepage URL |
| `documentation-website` | No | Documentation URL |
| `terms-url` | No | URL to Terms & Conditions |

## Quick start

### Pack a directory

```csharp
var (privateKey, publicKey) = PackageSigner.GenerateKeyPair();

var packer = new TwPackage();
using var output = File.Create("my-package.zip");
await packer.PackAsync("path/to/package-root", output,
    new PackOptions { PrivateKey = privateKey });
```

### Verify a package

```csharp
var packer = new TwPackage();
var result = await packer.VerifyAsync("my-package.zip",
    new VerifyOptions { PublicKey = publicKey });

if (!result.IsValid)
    foreach (var issue in result.Issues)
        Console.WriteLine($"{issue.Code}: {issue.Message}");
```

### Unpack (with automatic verification)

```csharp
using var input = File.OpenRead("my-package.zip");
await packer.UnpackAsync(input, "output/directory",
    new UnpackOptions { PublicKey = publicKey });
```

### Re-sign an existing package

```csharp
using var input = File.OpenRead("old.zip");
using var output = File.Create("new.zip");
await packer.ResignAsync(input, output, newPrivateKey);
```

## Signing algorithm

The default signing algorithm is ECDSA P-384 with SHA-384.
The algorithm name is stored in `signature.sig` to allow future migration (e.g. to Ed25519 or post-quantum ML-DSA).

## Customizing validation

Implement `IPackageValidator` to customize security checks:

```csharp
var packer = new TwPackage(myCustomValidator);
```

The default `PackageValidator` enforces: path traversal prevention, reserved name blocking, size limits, structure validation, completeness checks, and canonical signature ordering.

## CI packaging

For CI pipelines that don't have the full Tinkwell CLI, the [`tinkwell-ci-package`](../Tinkwell.Build.Ci/README.md) global tool creates `.twpkg` packages from a flat directory using this library under the hood.
