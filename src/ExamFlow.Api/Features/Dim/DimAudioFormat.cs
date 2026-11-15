namespace ExamFlow.Api.Features.Dim;

/// <summary>Recognises the audio formats every current browser can play, by their first bytes (not the file name).</summary>
public static class DimAudioFormat
{
    public const long MaxSizeBytes = 30 * 1024 * 1024;

    /// <summary>(content type, file extension) of a supported recording; null for anything else.</summary>
    public static (string ContentType, string Extension)? Detect(ReadOnlySpan<byte> head)
    {
        if (head.Length < 12) return null;
        // MP3: an ID3 tag, or straight into an MPEG audio frame (11 sync bits).
        if (head[..3].SequenceEqual("ID3"u8) || (head[0] == 0xFF && (head[1] & 0xE0) == 0xE0)) return ("audio/mpeg", ".mp3");
        // M4A / AAC in an MP4 container: "ftyp" box at offset 4.
        if (head[4..8].SequenceEqual("ftyp"u8)) return ("audio/mp4", ".m4a");
        if (head[..4].SequenceEqual("OggS"u8)) return ("audio/ogg", ".ogg");
        if (head[..4].SequenceEqual("RIFF"u8) && head[8..12].SequenceEqual("WAVE"u8)) return ("audio/wav", ".wav");
        return null;
    }
}
