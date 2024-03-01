﻿using System.Collections.Immutable;

namespace GBX.NET;

/// <summary>
/// [CSystemPackDesc] Description of a file reference. Also known as "fileref".
/// </summary>
public record PackDesc(string FilePath = "", ImmutableArray<byte> Checksum = default, string? LocatorUrl = "")
{
    /// <summary>
    /// A default instance of <see cref=""/> with empty values.
    /// </summary>
    public static readonly PackDesc Default = new();
    
    /// <summary>
    /// Gets the locator URI of the file reference.
    /// </summary>
    /// <returns>A Uri object if LocatorUrl is specified, otherwise null.</returns>
    public Uri? GetLocatorUri()
    {
        return string.IsNullOrEmpty(LocatorUrl) ? null : new Uri(LocatorUrl);
    }
}
