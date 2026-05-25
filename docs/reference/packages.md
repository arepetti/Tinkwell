# Tinkwell Package Format

A Tinkwell package is a zip file with a well-defined structure and an SHA-512 + ECDSA P-384 integrity chain.
Packages are the primary distribution format for [plugins](plugins.md).

## Structure

```
/
├── package.tw              # Manifest
├── content/                # All packaged files
│   └── ...
└── security/
    ├── signatures.tw       # SHA-512 hash + size of every file outside security/
    └── signature.sig       # ECDSA signature of SHA-512(signatures.tw)
```

- `package.tw` — Required.
  Describes the package (name, version, author, etc.).
- `content/` — Contains the actual payload files (DLLs, configs, assets).
- `security/` — Optional. When present, provides a tamper-detection chain.

## Manifest (`package.tw`)

The manifest is a `.tw` block named after the package:

```
package "my-plugin" {
  format-version = 1
  version = "1.0.0"
  author = "Jane Doe"
  description = "What this package contains"
  license = "MIT"
}
```

### Known properties

| Key | Required | Description |
|-----|----------|-------------|
| `name` | Yes | Package identifier (block name) |
| `format-version` | No | Manifest format version (default: 1) |
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

Any other key-value pair is a custom property, passed through to consumers as-is.
Plugins use the `product-version` custom property to declare Tinkwell version compatibility (see [plugins](plugins.md)).

## Security model

### Integrity chain

1. Every file outside `security/` has its SHA-512 hash and byte size recorded in `security/signatures.tw`, in canonical (sorted) order.
2. `security/signature.sig` contains the ECDSA P-384 signature of the SHA-512 hash of `signatures.tw`.

This means any modification to any file — including the manifest — is detectable.

### Validation checks

The default validator enforces:

- **Path traversal prevention** — No `..` or absolute paths in zip entries
- **Reserved name blocking** — No OS-reserved file names (CON, PRN, etc.)
- **Size limits** — Configurable per-file and total size limits to prevent zip bombs
- **Structure validation** — Only `package.tw`, `content/`, and `security/` at root level
- **Completeness checks** — Every file in the zip must appear in `signatures.tw` and vice versa
- **Canonical ordering** — Signature entries must be in sorted order

### Signing algorithm

The default algorithm is ECDSA P-384 with SHA-384.
The algorithm name is stored in `signature.sig` to allow future migration (e.g. to Ed25519 or post-quantum ML-DSA).

## CLI commands

Use `tw package` to work with packages:

- `tw package create-manifest` — Create a `package.tw` interactively or from arguments
- `tw package pack` — Pack a directory into a signed package
- `tw package unpack` — Extract a package (with optional verification)
- `tw package verify` — Verify integrity and signatures
- `tw package resign` — Re-sign with a new key
- `tw identity generate-key` — Generate an ECDSA P-384 key pair

See the [CLI reference](../user-guide/cli.md#package) for full usage.

## CI packaging (`tinkwell-ci-package`)

For CI pipelines that don't have the full Tinkwell CLI installed, the `tinkwell-ci-package` global tool creates `.twpkg` packages from a flat directory (e.g. `dotnet publish` output).

```bash
dotnet tool install -g Tinkwell.Build.Ci
tinkwell-ci-package pack ./staging -o my-plugin.twpkg --sign
```

The tool reads `package.tw` from the input directory, places it at the package root, and puts everything else under `content/`.
Optional signing uses a base64-encoded PKCS#8 key from an environment variable (`TW_SIGNING_KEY` by default).

See the [tool README](https://github.com/arepetti/Tinkwell/blob/main/src/app/libs/Tinkwell.Build.Ci/README.md) for full usage, GitHub Actions examples, and signing key generation.

## Library API

The `Tinkwell.Package` NuGet library provides programmatic access via the `TwPackage` class.
See the [library README](https://github.com/arepetti/Tinkwell/blob/main/src/app/libs/Tinkwell.Package/README.md) for C# examples.
