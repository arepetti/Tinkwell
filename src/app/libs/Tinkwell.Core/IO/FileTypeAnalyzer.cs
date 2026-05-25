namespace Tinkwell.IO;

/// <summary>
/// Identifies well-known binary file formats by inspecting the first four
/// bytes (magic number) and, for executables, the file extension.
/// </summary>
/// <remarks>
/// All methods are thread-safe: <see cref="Detect(string)"/> and
/// <see cref="IsExecutableOrObject(string)"/> open the file with normal
/// sharing; concurrent threads analyzing the same path interleave I/O
/// in the usual OS-defined way. <see cref="Detect(Stream)"/> is safe
/// as long as callers serialize access to a shared stream.
/// </remarks>
public static class FileTypeAnalyzer
{
    /// <summary>Known binary file formats detected by magic-number inspection.</summary>
    public enum FileType
    {
        /// <summary>Format not recognized.</summary>
        Unknown,
        /// <summary>WebAssembly module.</summary>
        Wasm,
        /// <summary>ZIP archive (including JAR, NuGet, etc.).</summary>
        Zip,
        /// <summary>ELF executable or shared object (Linux).</summary>
        Elf,
        /// <summary>PE/COFF executable (Windows).</summary>
        Pe,
        /// <summary>Mach-O executable or universal binary (macOS).</summary>
        MachO,
    }

    private static readonly HashSet<string> ExecutableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".so", ".dylib", ".sys", ".drv",
        ".o", ".obj", ".a", ".lib",
        ".ko", ".elf", ".bin", ".com", ".scr",
    };

    /// <summary>
    /// Detects the file type of a file on disk by reading its first four bytes.
    /// </summary>
    public static FileType Detect(string path)
    {
        Span<byte> header = stackalloc byte[4];
        using var stream = File.OpenRead(path);
        if (stream.Length < 4)
            return FileType.Unknown;

        stream.ReadExactly(header);
        return ClassifyHeader(header);
    }

    /// <summary>
    /// Detects the file type from a stream. If the stream supports seeking,
    /// the position is restored after reading.
    /// </summary>
    public static FileType Detect(Stream stream)
    {
        Span<byte> header = stackalloc byte[4];
        long originalPos = stream.CanSeek ? stream.Position : -1;

        if (stream.Read(header) < 4)
        {
            if (originalPos >= 0)
                stream.Position = originalPos;
            return FileType.Unknown;
        }

        if (originalPos >= 0)
            stream.Position = originalPos;
        return ClassifyHeader(header);
    }

    /// <summary>
    /// Returns <see langword="true"/> when the file at <paramref name="path"/>
    /// has a WebAssembly magic number.
    /// </summary>
    public static bool IsWasm(string path) => Detect(path) == FileType.Wasm;

    /// <summary>
    /// Returns <see langword="true"/> when the file at <paramref name="path"/>
    /// appears to be a native executable or object file, based on its extension
    /// or magic number.
    /// </summary>
    public static bool IsExecutableOrObject(string path)
    {
        var ext = Path.GetExtension(path);
        if (ExecutableExtensions.Contains(ext))
            return true;

        var type = Detect(path);
        return type is FileType.Elf or FileType.Pe or FileType.MachO;
    }

    private static FileType ClassifyHeader(ReadOnlySpan<byte> header)
    {
        if (header[0] == 0x00 && header[1] == 0x61 && header[2] == 0x73 && header[3] == 0x6D)
            return FileType.Wasm;
        if (header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04)
            return FileType.Zip;
        if (header[0] == 0x7F && header[1] == 0x45 && header[2] == 0x4C && header[3] == 0x46)
            return FileType.Elf;
        if (header[0] == 0x4D && header[1] == 0x5A)
            return FileType.Pe;
        if (header[0] == 0xFE && header[1] == 0xED && header[2] == 0xFA && (header[3] == 0xCE || header[3] == 0xCF))
            return FileType.MachO;
        if ((header[0] == 0xCE || header[0] == 0xCF) && header[1] == 0xFA && header[2] == 0xED && header[3] == 0xFE)
            return FileType.MachO;
        if (header[0] == 0xCA && header[1] == 0xFE && header[2] == 0xBA && header[3] == 0xBE)
            return FileType.MachO;
        if (header[0] == 0xBE && header[1] == 0xBA && header[2] == 0xFE && header[3] == 0xCA)
            return FileType.MachO;

        return FileType.Unknown;
    }
}
