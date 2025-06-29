# Ensamble Syntax Highlighting for VS Code

This extension provides syntax highlighting support for Tinkwell configuration files. Files currently supported are:
* `.ensamble`, `.tw`: ensamble configuration files.
* `.twm`: reducer configuration files.

## 1. Clone or download this repository

Make sure the folder contains:
- `package.json`
- `language-configuration.json`
- `syntaxes/tw.tmLanguage.json`
- `syntaxes/twm.tmLanguage.json`

## 2. Package the extension

Install the [VSCE](https://code.visualstudio.com/api/working-with-extensions/publishing-extension) CLI tool if you don’t have it:

```bash
npm install -g vsce
```

Go to the `tinkwell-ensamble-syntax` folder and create the package:

```bash
vsce package
```

If you do not want to install the VSCE package then you can run this instead:

```bash
npx vsce package
```

## 3. Install the extension in VSCode

Using the UI:

- Open Visual Studio Code
- Go to Extensions view (Ctrl+Shift+X)
- Click the three-dot menu in the top-right corner
- Choose "Install from VSIX..."
- Select the .vsix file you just created

Alternatively, from the command line:

```bash
code --install-extension path/to/ensamble-syntax-0.0.2.vsix
```