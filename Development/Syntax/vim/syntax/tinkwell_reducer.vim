" Tinkwell Reducer syntax highlighting

if exists("b:current_syntax")
  finish
endif

syntax keyword Keyword import measure type unit expression description minimum maximum tags category precision define if
syntax keyword reducerBoolean true false
syntax match reducerString /"\(\\.\|[^"]\)*"/
syntax match reducerComment "//.*$"
syntax match reducerBrace /[{}]/
syntax match reducerColon /:/
syntax match reducerIdentifier /\<[A-Za-z_][A-Za-z0-9_]*\>/

highlight link reducerKeyword Keyword
highlight link reducerBoolean Boolean
highlight link reducerString String
highlight link reducerComment Comment
highlight link reducerBrace Delimiter
highlight link reducerColon Operator
highlight link reducerIdentifier Identifier

let b:current_syntax = "tinkwell_reducer"