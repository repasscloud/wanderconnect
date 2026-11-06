namespace Swoovr.Contracts;

/// <summary>
/// Member support attachments (owner, issue 220, D330): the owner's list of file types and limits, shared by the API
/// (which enforces it and checks each file's content matches its type) and the member web app (which checks first so a
/// member hears straight away). The content type is always derived from the extension here, never taken from the sender.
/// </summary>
public static class SupportAttachmentPolicy
{
    public enum Kind { Image, Document, Text, Video, Audio }

    private static readonly Dictionary<string, (Kind Kind, string Mime)> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        // Images
        [".jpg"] = (Kind.Image, "image/jpeg"), [".jpeg"] = (Kind.Image, "image/jpeg"), [".png"] = (Kind.Image, "image/png"),
        [".webp"] = (Kind.Image, "image/webp"), [".heic"] = (Kind.Image, "image/heic"), [".heif"] = (Kind.Image, "image/heif"),
        [".gif"] = (Kind.Image, "image/gif"),
        // Documents
        [".pdf"] = (Kind.Document, "application/pdf"), [".doc"] = (Kind.Document, "application/msword"),
        [".docx"] = (Kind.Document, "application/vnd.openxmlformats-officedocument.wordprocessingml.document"),
        [".xls"] = (Kind.Document, "application/vnd.ms-excel"),
        [".xlsx"] = (Kind.Document, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"),
        [".csv"] = (Kind.Document, "text/csv"),
        // Text and diagnostics
        [".txt"] = (Kind.Text, "text/plain"), [".log"] = (Kind.Text, "text/plain"), [".md"] = (Kind.Text, "text/markdown"),
        // Video
        [".mp4"] = (Kind.Video, "video/mp4"), [".mov"] = (Kind.Video, "video/quicktime"), [".webm"] = (Kind.Video, "video/webm"),
        // Audio
        [".mp3"] = (Kind.Audio, "audio/mpeg"), [".m4a"] = (Kind.Audio, "audio/mp4"), [".wav"] = (Kind.Audio, "audio/wav"),
    };

    public const int MaxAttachmentsPerMessage = 5;
    public const long MaxImageBytes = 15 * 1024 * 1024;
    public const long MaxDocumentBytes = 20 * 1024 * 1024;
    public const long MaxVideoBytes = 100 * 1024 * 1024;
    public const long MaxAudioBytes = 25 * 1024 * 1024;
    public const long MaxTotalMessageBytes = 150 * 1024 * 1024;
    /// <summary>A multipart request: every file at the total limit, the text and the form overhead.</summary>
    public const long MaxUploadRequestBytes = MaxTotalMessageBytes + 1024 * 1024;

    public static IReadOnlyCollection<string> AllowedExtensions => Types.Keys;

    /// <summary>The file input's accept list.</summary>
    public static string Accept => string.Join(",", Types.Keys);

    public static string Extension(string filename) => Path.GetExtension(filename.Trim()).ToLowerInvariant();

    public static (Kind Kind, string Mime)? TypeOf(string filename) => Types.TryGetValue(Extension(filename), out var type) ? type : null;

    public static long MaxBytes(Kind kind) => kind switch
    {
        Kind.Image => MaxImageBytes,
        Kind.Video => MaxVideoBytes,
        Kind.Audio => MaxAudioBytes,
        _ => MaxDocumentBytes, // documents and text
    };

    /// <summary>Whether one stored attachment may be shown to members (type and size), e.g. in a ticket's thread.</summary>
    public static bool Allowed(string filename, long size) => TypeOf(filename) is { } type && size > 0 && size <= MaxBytes(type.Kind);

    /// <summary>The first problem with a set of files, in words a member reads, or null when they can be sent.</summary>
    public static string? Problem(IReadOnlyCollection<(string Name, long Size)> files)
    {
        if (files.Count > MaxAttachmentsPerMessage) return $"Add up to {MaxAttachmentsPerMessage} files to a message.";
        foreach (var (name, size) in files)
        {
            if (TypeOf(name) is not { } type) return $"{name} can't be attached. Use a photo, PDF, Word or Excel file, text, video or audio.";
            if (size <= 0) return $"{name} is empty.";
            if (size > MaxBytes(type.Kind)) return $"{name} is larger than {Megabytes(MaxBytes(type.Kind))} MB.";
        }
        return files.Sum(f => f.Size) > MaxTotalMessageBytes ? $"Files in one message can't add up to more than {Megabytes(MaxTotalMessageBytes)} MB." : null;
    }

    public static string Size(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} bytes",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
        _ => $"{bytes / (1024.0 * 1024):0.#} MB",
    };

    private static long Megabytes(long bytes) => bytes / (1024 * 1024);
}
