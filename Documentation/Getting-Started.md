# Getting Started with Tinkwell

This guide provides a step-by-step walkthrough to get a basic Tinkwell instance up and running.

Before you start:
- Check your firewall, Tinkwell hosts should be allowed to receive data on the ports they're listening to! Whenever possible Tinkwell will try to open the ports for you but if you have any communication problem then it's the first thing to check.
- On Linux/OSX/BSD give read/write permissions to the user running the app to the directory `"/var/lib/Tinkwell"`, if it's not writeable then the application will default to `"~/.local/share/Tinkwell"` for both application and user data.

## 1. Generate the Development Certificate

Before running the Supervisor, you need a valid HTTPS certificate. If you have a valid PFX certificate then set its path in `TINKWELL_CERT_PATH` and its password in `TINKWELL_CERT_PASS`. If you do not have one or you want to start experimenting/developing using a self-signed certificate then follow these instructions.

### Windows

Go the application folder and run:

```bash
./tw certs create
./tw certs install
```

If you're working with a machine without Tinkwell then you can use the script `Development/Create-DevCert.ps1` and then set the environment variables manually. 

### Linux

* Create the certificate using `tw` (alternatively you could use the script `Development/create-dev-cert.sh`, be sure to update to change file names as appropriate):
    ```bash
    ./tw certs create --export-path=. --export-name=tinkwell
    ```

* Add these lines to the bottom of your shell profile script (e.g. `~/.bashrc` or `~/.zshrc`):
    ```bash
    export TINKWELL_CERT_PATH="/absolute/path/to/tinkwell.pfx"
    export TINKWELL_CERT_PASS="your-password-here"
    ```

#### Debian (Ubuntu, Linux Mint, etc)

```bash
sudo cp ./tinkwell-cert.pem /usr/local/share/ca-certificates/tinkwell-cert.crt
sudo update-ca-certificates
```

#### Red Hat (RHEL, CentOS, Fedora)

```bash
sudo cp ./tinkwell-cert.pem /etc/pki/ca-trust/source/anchors/tinkwell-cert.crt
sudo update-ca-trust extract
```

#### Arch Linux

```bash
sudo cp ./tinkwell-cert.pem /etc/ca-certificates/trust-source/anchors/tinkwell-cert.crt
sudo update-ca-trust extract
```

Alternatively:

```bash
mv ./tinkwell-cert.pem ./tinkwell-cert.crt
sudo trust anchor --store ./tinkwell-cert.crt
```

## 2. Understand the Ensemble Configuration

The `ensamble.tw` file is the heart of your application's configuration, defining all the processes (runners) that the Supervisor will manage.

-   Review the [Ensemble Syntax documentation](./Ensamble.md) to understand how runners and firmlets are defined.
-   Open the default `ensamble.tw` file (in the `Assets` project if in VS or the output folder if in "production") to see a practical example.

You can create a starting point with all the basic features already configured using `tw templates create`. After you modified the configuration you should run the linters to catch common mistakes:

```bash
./tw ensamble lint ./ensamble.tw
./tw measures lint ./config/measures.twm
./tw actions lint ./config/actions.twa
```

## 3. Start the Supervisor

The Supervisor is the main entry point of your application: launch the `Tinkwell.Supervisor`.

## 4. Verify the System

Once the Supervisor is running, you can use the command-line interface (CLI) to inspect the system. Check the console log for errors then type these commands:

```bash
# Check that all the basic runners are correctly loaded and
# gRPC services are working as expected.
./tw runners list
./tw contracts list
```

## Next Steps

You now have a running Tinkwell system. To learn more, explore the following resources:

- [Glossary](./Glossary.md): Understand the core concepts and terminology.
- [CLI Reference](./CLI.md): Discover all the available commands for monitoring and debugging.
- [Derived Measures](./Derived-measures.md): Learn how to create custom measures.
- [How-To](./How-to.md): Quick guide for doing the most common tasks.
