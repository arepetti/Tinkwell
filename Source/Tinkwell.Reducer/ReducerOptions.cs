﻿namespace Tinkwell.Reducer;

sealed class ReducerOptions
{
    public required string Path { get; init; }

    public bool UseConstants { get; set; }
}
