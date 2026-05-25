# Tinkwell.Build.Ci

A lightweight .NET global tool for packaging Tinkwell plugins and extensions in CI pipelines.
No full Tinkwell installation required.

## Install

```bash
dotnet tool install -g Tinkwell.Build.Ci
```

## Commands

### `pack`

Creates a `.twpkg` from a flat directory (e.g. `dotnet publish` output).
The directory must contain a `package.tw` manifest; everything else becomes the `content/` of the package.

```
tinkwell-ci-package pack <directory> -o <output.twpkg> [--sign] [--key-env <VAR>]
```

| Option             | Description                                                  |
| ------------------ | ------------------------------------------------------------ |
| `<directory>`      | Input directory containing `package.tw` and all content      |
| `-o, --output`     | Output `.twpkg` file path                                    |
| `--sign`           | Sign the package with an ECDSA P-384 key                     |
| `--key-env <VAR>`  | Environment variable with base64-encoded PKCS#8 key (default: `TW_SIGNING_KEY`) |

### Examples

```bash
# Unsigned (local dev)
tinkwell-ci-package pack ./bin/Release/net10.0/publish -o my-plugin.twpkg

# Signed (CI)
tinkwell-ci-package pack ./staging -o ./dist/my-plugin.twpkg --sign
```

## GitHub Actions usage

```yaml
- name: Install tinkwell-ci-package
  run: dotnet tool install -g Tinkwell.Build.Ci

- name: Publish
  run: dotnet publish src/MyPlugin -c Release -o ./staging

- name: Package
  run: tinkwell-ci-package pack ./staging -o ./dist/my-plugin.twpkg --sign
  env:
    TW_SIGNING_KEY: ${{ secrets.TW_SIGNING_KEY }}
```

## Generating a signing key

```bash
# Generate a new ECDSA P-384 key pair
openssl ecparam -genkey -name secp384r1 -noout | openssl pkcs8 -topk8 -nocrypt -outform DER | base64 -w0
```

Store the output as a GitHub Actions secret named `TW_SIGNING_KEY`.

## Cross-version compatibility

Built targeting .NET 10 with `rollForward: latestMajor`.
Works on any .NET 10+ SDK without rebuilding.
