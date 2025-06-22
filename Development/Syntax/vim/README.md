# Ensamble Syntax Highlighting for Vim

This Vim syntax file adds color highlighting support for `.ensamble` configuration files.

## Installation

From the `vim` directory in this repository:

1. **Create Vim directories (if needed):**
   ```bash
   mkdir -p ~/.vim/syntax
   mkdir -p ~/.vim/ftdetect
   ```
2. **Copy the syntax file and make the association:**
   ```bash
   cp ./syntax/ensamble.vim ~/.vim/syntax/ensamble.vim
   cp ./ftdetect/ensamble.vim ~/.vim/ftdetect/ensamble.vim
   ```
3. **Enable syntax highlighting: in your `~/vimrc` ensure this is present:**
   ```vim
   syntax on
   filetype plugin indent on
   ```
