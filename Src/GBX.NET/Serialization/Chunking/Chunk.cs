﻿namespace GBX.NET.Serialization.Chunking;

/// <summary>
/// A data chunk.
/// </summary>
public interface IChunk
{
    /// <summary>
    /// ID of the chunk.
    /// </summary>
    uint Id { get; }

    IChunk DeepClone();
}

/// <summary>
/// A data chunk.
/// </summary>
public abstract class Chunk : IReadableWritableChunk
{
    /// <inheritdoc />
    public abstract uint Id { get; }

    /// <inheritdoc />
    public virtual void ReadWrite(IClass n, GbxReaderWriter rw)
    {
        if (rw.Reader is not null)
        {
            Read(n, rw.Reader);
        }

        if (rw.Writer is not null)
        {
            Write(n, rw.Writer);
        }
    }

    /// <inheritdoc />
    public virtual void Read(IClass n, GbxReader r) { }

    /// <inheritdoc />
    public virtual void Write(IClass n, GbxWriter w) { }

    public abstract IChunk DeepClone();
}