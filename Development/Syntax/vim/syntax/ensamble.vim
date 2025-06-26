" Ensamble syntax highlighting

if exists("b:current_syntax")
  finish
endif

syntax keyword ensambleKeyword import runner service argument arguments activation properties if
syntax keyword ensambleBoolean true false
syntax match ensambleString /"\(\\.\|[^"]\)*"/
syntax match ensambleComment "//.*$"
syntax match ensambleBrace /[{}]/
syntax match ensambleColon /:/
syntax match ensambleIdentifier /\<[A-Za-z_][A-Za-z0-9_]*\>/

highlight link ensambleKeyword Keyword
highlight link ensambleBoolean Boolean
highlight link ensambleString String
highlight link ensambleComment Comment
highlight link ensambleBrace Delimiter
highlight link ensambleColon Operator
highlight link ensambleIdentifier Identifier

let b:current_syntax = "ensamble"