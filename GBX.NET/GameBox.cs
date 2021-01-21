﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace GBX.NET
{
    public enum GameBoxReadProgressStage
    {
        Header,
        HeaderUserData,
        RefTable,
        Body
    }

    /// <summary>
    /// A known serialized GameBox node with additional attributes. This class can represent deserialized .Gbx file.
    /// </summary>
    /// <typeparam name="T">The main node of the GBX.</typeparam>
    public class GameBox<T> : GameBox where T : Node
    {
        /// <summary>
        /// Header part, typically storing metadata for quickest access.
        /// </summary>
        public new GameBoxHeader<T> Header
        {
            get => base.Header as GameBoxHeader<T>;
            set => base.Header = value;
        }

        /// <summary>
        /// Body part, storing information about the node that realistically affects the game.
        /// </summary>
        public GameBoxBody<T> Body { get; private set; }

        /// <summary>
        /// Node containing data taken from the body part.
        /// </summary>
        public T MainNode { get; internal set; }

        /// <summary>
        /// Constructs an empty GameBox object.
        /// </summary>
        public GameBox()
        {
            Game = ClassIDRemap.ManiaPlanet;
        }

        /// <summary>
        /// Creates a header chunk based on the attributes from <typeparamref name="TChunk"/>.
        /// </summary>
        /// <typeparam name="TChunk">A chunk type compatible with <see cref="IHeaderChunk"/>.</typeparam>
        /// <returns>A newly created chunk.</returns>
        public TChunk CreateHeaderChunk<TChunk>() where TChunk : Chunk, IHeaderChunk
        {
            return Header.Chunks.Create<TChunk>();
        }

        /// <summary>
        /// Removes all header chunks.
        /// </summary>
        public void RemoveAllHeaderChunks()
        {
            Header.Chunks.Clear();
        }

        /// <summary>
        /// Removes a header chunk based on the attributes from <typeparamref name="TChunk"/>.
        /// </summary>
        /// <typeparam name="TChunk">A chunk type compatible with <see cref="IHeaderChunk"/>.</typeparam>
        /// <returns>True, if the chunk was removed, otherwise false.</returns>
        public bool RemoveHeaderChunk<TChunk>() where TChunk : Chunk, IHeaderChunk
        {
            return Header.Chunks.RemoveWhere(x => x.ID == typeof(TChunk).GetCustomAttribute<ChunkAttribute>().ID) > 0;
        }

        /// <summary>
        /// Creates a body chunk based on the attributes from <typeparamref name="TChunk"/>.
        /// </summary>
        /// <typeparam name="TChunk">A chunk type that isn't a header chunk.</typeparam>
        /// <param name="data">If the chunk is <see cref="ISkippableChunk"/>, the bytes to initiate the chunks with, unparsed. If it's not a skippable chunk, data is parsed immediately.</param>
        /// <returns>A newly created chunk.</returns>
        public TChunk CreateBodyChunk<TChunk>(byte[] data) where TChunk : Chunk
        {
            return MainNode.Chunks.Create<TChunk>(data);
        }

        /// <summary>
        /// Creates a body chunk based on the attributes from <typeparamref name="TChunk"/> with no inner data provided.
        /// </summary>
        /// <typeparam name="TChunk">A chunk type that isn't a header chunk.</typeparam>
        /// <returns>A newly created chunk.</returns>
        public TChunk CreateBodyChunk<TChunk>() where TChunk : Chunk
        {
            return CreateBodyChunk<TChunk>(new byte[0]);
        }

        /// <summary>
        /// Removes all body chunks.
        /// </summary>
        public void RemoveAllBodyChunks()
        {
            MainNode.Chunks.Clear();
        }

        /// <summary>
        /// Removes a body chunk based on the attributes from <typeparamref name="TChunk"/>.
        /// </summary>
        /// <typeparam name="TChunk">A chunk type that isn't a header chunk.</typeparam>
        /// <returns>True, if the chunk was removed, otherwise false.</returns>
        public bool RemoveBodyChunk<TChunk>() where TChunk : Chunk
        {
            return MainNode.Chunks.Remove<TChunk>();
        }

        /// <summary>
        /// Discover all skippable BODY chunks.
        /// </summary>
        public void DiscoverAllChunks()
        {
            //foreach (var chunk in Header.Result.Chunks.Values)
            //    chunk.Discover();
            MainNode.DiscoverAllChunks();
        }

        public override bool ReadHeader(GameBoxReader reader, IProgress<GameBoxReadProgress> progress)
        {
            var parameters = new GameBoxHeaderParameters(this);
            if (!parameters.Read(reader)) return false; // Should already throw an exception

            progress?.Report(new GameBoxReadProgress(GameBoxReadProgressStage.Header, 1, this));

            if (ClassID != typeof(T).GetCustomAttribute<NodeAttribute>().ID)
            {
                if (!Node.Names.TryGetValue(ClassID.Value, out string name))
                    name = "unknown class";
                throw new InvalidCastException($"GBX with ID 0x{ClassID:X8} ({name}) can't be casted to GameBox<{typeof(T).Name}>.");
            }

            MainNode = Activator.CreateInstance<T>();

            Log.Write("Working out the header chunks...");

            try
            {
                Header = new GameBoxHeader<T>(this, parameters.UserData);
                Header.Read(progress);
                Log.Write("Header chunks parsed without any exceptions.", ConsoleColor.Green);
            }
            catch (Exception e)
            {
                Log.Write("Header chunks parsed with exceptions.", ConsoleColor.Red);
                Log.Write(e.ToString(), ConsoleColor.Red);
            }

            return true;
        }

        protected override bool Read(GameBoxReader reader, IProgress<GameBoxReadProgress> progress)
        {
            // Header

            Log.Write("Reading the header...");

            if (!ReadHeader(reader, progress))
                return false;

            // Reference table

            Log.Write("Reading the reference table...");

            ReadRefTable(reader, progress);

            // Body

            Log.Write("Reading the body...");

            switch (BodyCompression)
            {
                case 'C':
                    var uncompressedSize = reader.ReadInt32();
                    var compressedSize = reader.ReadInt32();

                    var data = reader.ReadBytes(compressedSize);
                    Body = new GameBoxBody<T>(this);
                    Body.Read(data, uncompressedSize, progress);

                    break;
                case 'U':
                    var uncompressedData = reader.ReadToEnd();
                    Body = new GameBoxBody<T>(this);
                    Body.Read(uncompressedData, progress);

                    break;
                default:
                    return false;
            }

            Log.Write("Body completed!");

            return true;
        }

        public void Write(GameBoxWriter w, ClassIDRemap remap)
        {
            using (MemoryStream ms = new MemoryStream())
            using (GameBoxWriter bodyW = new GameBoxWriter(ms))
            {
                (Body as ILookbackable).IdWritten = false;
                (Body as ILookbackable).IdStrings.Clear();
                Body.AuxilaryNodes.Clear();

                Log.Write("Writing the body...");

                Body.Write(bodyW, remap); // Body is written first so that the aux node count is determined properly

                Log.Write("Writing the header...");

                (Header as ILookbackable).IdWritten = false;
                (Header as ILookbackable).IdStrings.Clear();
                Header.Write(w, Body.AuxilaryNodes.Count + 1, remap);

                Log.Write("Writing the reference table...");

                if (RefTable == null)
                    w.Write(0);
                else
                    RefTable.Write(w);

                w.Write(ms.ToArray(), 0, (int)ms.Length);
            }
        }

        public void Write(GameBoxWriter w)
        {
            Write(w, ClassIDRemap.Latest);
        }

        /// <summary>
        /// Saves the serialized GameBox to a stream.
        /// </summary>
        /// <param name="stream">Any kind of stream that supports writing.</param>
        /// <param name="remap">What to remap the newest node IDs to. Used for older games.</param>
        public void Save(Stream stream, ClassIDRemap remap)
        {
            if (IntPtr.Size == 8)
                throw new NotSupportedException("Saving GBX is not supported with x64 platform target, due to LZO compression bug. Please force your platform target to x86.");

            using (var w = new GameBoxWriter(stream))
                Write(w, remap);
        }

        /// <summary>
        /// Saves the serialized GameBox to a stream.
        /// </summary>
        /// <param name="stream">Any kind of stream that supports writing.</param>
        public void Save(Stream stream)
        {
            Save(stream, ClassIDRemap.Latest);
        }

        /// <summary>
        /// Saves the serialized GameBox on a disk.
        /// </summary>
        /// <param name="fileName">Relative or absolute file path.</param>
        /// <param name="remap">What to remap the newest node IDs to. Used for older games.</param>
        public void Save(string fileName, ClassIDRemap remap)
        {
            using (var fs = File.OpenWrite(fileName))
                Save(fs, remap);

            Log.Write($"GBX file {fileName} saved.");
        }

        /// <summary>
        /// Saves the serialized GameBox on a disk.
        /// </summary>
        /// <param name="fileName">Relative or absolute file path.</param>
        public void Save(string fileName)
        {
            Save(fileName, ClassIDRemap.Latest);
        }
    }

    /// <summary>
    /// An unknown serialized GameBox node with additional attributes. This class can represent deserialized .Gbx file.
    /// </summary>
    public class GameBox : IGameBox
    {
        public ClassIDRemap Game { get; set; }

        public short Version { get; set; }
        public char? ByteFormat { get; set; }
        public char? RefTableCompression { get; set; }
        public char? BodyCompression { get; set; }
        public char? UnknownByte { get; set; }
        public uint? ClassID { get; internal set; }
        public int? NumNodes { get; internal set; }

        /// <summary>
        /// Header part, typically storing metadata for quickest access.
        /// </summary>
        public GameBoxHeader Header { get; set; }
        /// <summary>
        /// Reference table, referencing other GBX.
        /// </summary>
        public GameBoxRefTable RefTable { get; private set; }

        public string FileName { get; set; }

        public virtual bool ReadHeader(GameBoxReader reader, IProgress<GameBoxReadProgress> progress)
        {
            var parameters = new GameBoxHeaderParameters(this);
            if (!parameters.Read(reader)) return false;

            progress?.Report(new GameBoxReadProgress(GameBoxReadProgressStage.Header, 1, this));

            Log.Write("Working out the header chunks...");

            try
            {
                Header = new GameBoxHeader(this, parameters.UserData);
                Log.Write("Header chunks parsed without any exceptions.", ConsoleColor.Green);
            }
            catch (Exception e)
            {
                Log.Write("Header chunks parsed with exceptions.", ConsoleColor.Red);
                Log.Write(e.ToString(), ConsoleColor.Red);
            }

            progress?.Report(new GameBoxReadProgress(GameBoxReadProgressStage.HeaderUserData, 1, this));

            return true;
        }

        protected bool ReadRefTable(GameBoxReader reader, IProgress<GameBoxReadProgress> progress)
        {
            var numExternalNodes = reader.ReadInt32();

            if (numExternalNodes > 0)
            {
                var ancestorLevel = reader.ReadInt32();

                GameBoxRefTableFolder rootFolder = new GameBoxRefTableFolder("Root");

                var numSubFolders = reader.ReadInt32();
                ReadRefTableFolders(numSubFolders, ref rootFolder);

                void ReadRefTableFolders(int n, ref GameBoxRefTableFolder folder)
                {
                    for (var i = 0; i < n; i++)
                    {
                        var name = reader.ReadString();
                        var numSubSubFolders = reader.ReadInt32();

                        var f = new GameBoxRefTableFolder(name, folder);
                        folder.Folders.Add(f);

                        ReadRefTableFolders(numSubSubFolders, ref f);
                    }
                }

                var externalNodes = new ExternalNode[numExternalNodes];

                for (var i = 0; i < numExternalNodes; i++)
                {
                    string fileName = null;
                    int? resourceIndex = null;
                    bool? useFile = null;
                    int? folderIndex = null;

                    var flags = reader.ReadInt32();

                    if ((flags & 4) == 0)
                        fileName = reader.ReadString();
                    else
                        resourceIndex = reader.ReadInt32();

                    var nodeIndex = reader.ReadInt32();

                    if (Version >= 5)
                        useFile = reader.ReadBoolean();

                    if ((flags & 4) == 0)
                        folderIndex = reader.ReadInt32();

                    var extNode = new ExternalNode(flags, fileName, resourceIndex, nodeIndex, useFile, folderIndex);
                    externalNodes[i] = extNode;
                }

                var refTable = new GameBoxRefTable(rootFolder, externalNodes);
                RefTable = refTable;
            }
            else
                Log.Write("No external nodes found, reference table completed.", ConsoleColor.Green);

            progress?.Report(new GameBoxReadProgress(GameBoxReadProgressStage.RefTable, 1, this));

            return true;
        }

        protected virtual bool Read(GameBoxReader reader, IProgress<GameBoxReadProgress> progress)
        {
            return ReadHeader(reader, progress) && ReadRefTable(reader, progress);
        }

        public bool Read(Stream stream, IProgress<GameBoxReadProgress> progress)
        {
            using (GameBoxReader reader = new GameBoxReader(stream))
                return Read(reader, progress);
        }

        [Obsolete]
        public bool Load(string fileName)
        {
            Log.Write($"Loading {fileName}...");

            FileName = fileName;

            using (var fs = File.OpenRead(fileName))
            {
                var success = Read(fs, null);

                if (success) Log.Write($"Loaded {fileName}!", ConsoleColor.Green);
                else Log.Write($"File {fileName} has't loaded successfully.", ConsoleColor.Red);

                return success;
            }
        }

        [Obsolete]
        public bool Load(string fileName, bool loadToMemory)
        {
            if (loadToMemory)
            {
                using (var ms = new MemoryStream(File.ReadAllBytes(fileName)))
                    return Read(ms, null);
            }

            return Load(fileName);
        }

        public static GameBox ParseHeader(string fileName, IProgress<GameBoxReadProgress> progress = null)
        {
            using (var fs = File.OpenRead(fileName))
            {
                var gbx = ParseHeader(fs, progress);
                gbx.FileName = fileName;
                return gbx;
            }
        }

        public static GameBox ParseHeader(Stream stream, IProgress<GameBoxReadProgress> progress = null)
        {
            using (var r = new GameBoxReader(stream))
                return ParseHeader(r, progress);
        }

        public static GameBox ParseHeader(GameBoxReader reader, IProgress<GameBoxReadProgress> progress = null)
        {
            var classID = ReadClassID(reader);

            if (classID.HasValue)
            {
                var modernID = classID.GetValueOrDefault();
                if (Node.Mappings.TryGetValue(classID.GetValueOrDefault(), out uint newerClassID))
                    modernID = newerClassID;

                Debug.WriteLine("Parse: " + modernID.ToString("x8"));

                Node.AvailableClasses.TryGetValue(modernID, out Type availableClass);

                GameBox gbx;

                if (availableClass == null)
                    gbx = new GameBox();
                else
                    gbx = (GameBox)Activator.CreateInstance(typeof(GameBox<>).MakeGenericType(availableClass), true);

                reader.BaseStream.Seek(0, SeekOrigin.Begin);

                if (gbx.ReadHeader(reader, progress) && gbx.ReadRefTable(reader, progress))
                    return gbx;
            }

            Type GetBaseType(Type t)
            {
                if (t == null)
                    return null;
                if (t.BaseType == typeof(Node))
                    return t.BaseType;
                return GetBaseType(t.BaseType);
            }

            return null;
        }

        public static GameBox<T> ParseHeader<T>(Stream stream, IProgress<GameBoxReadProgress> progress = null) where T : Node
        {
            GameBox<T> gbx = new GameBox<T>();

            using (var r = new GameBoxReader(stream))
                if (gbx.ReadHeader(r, progress) && gbx.ReadRefTable(r, progress))
                    return gbx;

            return null;
        }

        public static GameBox<T> ParseHeader<T>(string fileName, IProgress<GameBoxReadProgress> progress = null) where T : Node
        {
            using (var fs = File.OpenRead(fileName))
            {
                var gbx = ParseHeader<T>(fs, progress);
                gbx.FileName = fileName;
                return gbx;
            }
        }

        /// <summary>
        /// Easily parses GBX format.
        /// </summary>
        /// <typeparam name="T">A known type of the GBX file. Unmatching type will throw an exception.</typeparam>
        /// <param name="stream">Stream to read GBX format from.</param>
        /// <param name="progress">Callback that reports any read progress.</param>
        /// <returns>A GameBox with specified main node type.</returns>
        public static GameBox<T> Parse<T>(Stream stream, IProgress<GameBoxReadProgress> progress = null) where T : Node
        {
            GameBox<T> gbx = new GameBox<T>();
            if (gbx.Read(stream, progress))
                return gbx;
            return null;
        }

        /// <summary>
        /// Easily parses a GBX file.
        /// </summary>
        /// <typeparam name="T">A known type of the GBX file. Unmatching type will throw an exception.</typeparam>
        /// <param name="fileName">Relative or absolute file path.</param>
        /// <param name="progress">Callback that reports any read progress.</param>
        /// <returns>A GameBox with specified main node type.</returns>
        /// <exception cref="InvalidCastException"/>
        /// <example>
        /// var gbx = GameBox.Parse&lt;CGameCtnChallenge&gt;("MyMap.Map.Gbx");
        /// // Node data is available in gbx.MainNode
        /// </example>
        public static GameBox<T> Parse<T>(string fileName, IProgress<GameBoxReadProgress> progress = null) where T : Node
        {
            using (var fs = File.OpenRead(fileName))
            {
                var gbx = Parse<T>(fs, progress);
                if (gbx == null) return null;
                gbx.FileName = fileName;
                return gbx;
            }
        }

        public static uint? ReadClassID(GameBoxReader reader)
        {
            uint? classID = null;

            if (reader.ReadString("GBX".Length) != "GBX") // If the file doesn't have GBX magic
                return null;

            var version = reader.ReadInt16(); // Version

            if (version >= 3)
            {
                reader.ReadByte();
                reader.ReadByte();
                reader.ReadByte();

                if (version >= 4)
                    reader.ReadByte();

                classID = reader.ReadUInt32();
            }

            return classID;
        }

        public static uint? ReadClassID(Stream stream)
        {
            using (GameBoxReader r = new GameBoxReader(stream))
                return ReadClassID(r);
        }

        public static uint? ReadClassID(string fileName)
        {
            using (var fs = File.OpenRead(fileName))
                return ReadClassID(fs);
        }

        /// <summary>
        /// Easily parses a GBX file.
        /// </summary>
        /// <param name="fileName">Relative or absolute file path.</param>
        /// <param name="progress">Callback that reports any read progress.</param>
        /// <returns>A GameBox with either basic information only (if unknown), or also with specified main node type (available by using an explicit <see cref="GameBox{T}"/> cast.</returns>
        /// <example>
        /// var gbx = GameBox.Parse("MyMap.Map.Gbx");
        /// 
        /// if (gbx is GameBox&lt;CGameCtnChallenge&gt; gbxMap)
        /// {
        ///     // Node data is available in gbxMap.MainNode
        /// }
        /// else if (gbx is GameBox&lt;CGameCtnReplayRecord&gt; gbxReplay)
        /// {
        ///     // Node data is available in gbxReplay.MainNode
        /// }
        /// </example>
        public static GameBox Parse(string fileName, IProgress<GameBoxReadProgress> progress = null)
        {
            using (var fs = File.OpenRead(fileName))
            {
                var gbx = Parse(fs, progress);
                if (gbx == null) return null;
                gbx.FileName = fileName;
                return gbx;
            }
        }

        /// <summary>
        /// Easily parses GBX format. The stream MUST support seeking.
        /// </summary>
        /// <param name="stream">Stream to read GBX format from.</param>
        /// <param name="progress">Callback that reports any read progress.</param>
        /// <exception cref="NotSupportedException"/>
        /// <returns>A GameBox with either basic information only (if unknown), or also with specified main node type (available by using an explicit <see cref="GameBox{T}"/> cast.</returns>
        public static GameBox Parse(Stream stream, IProgress<GameBoxReadProgress> progress = null)
        {
            using (var r = new GameBoxReader(stream))
            {
                var classID = ReadClassID(r);

                if (classID.HasValue)
                {
                    var modernID = classID.GetValueOrDefault();
                    if (Node.Mappings.TryGetValue(classID.GetValueOrDefault(), out uint newerClassID))
                        modernID = newerClassID;

                    Debug.WriteLine("Parse: " + modernID.ToString("x8"));

                    Node.AvailableClasses.TryGetValue(modernID, out Type availableClass);

                    GameBox gbx;

                    if (availableClass == null)
                        gbx = new GameBox();
                    else
                        gbx = (GameBox)Activator.CreateInstance(typeof(GameBox<>).MakeGenericType(availableClass), true);

                    stream.Seek(0, SeekOrigin.Begin);

                    if (gbx.Read(stream, progress))
                        return gbx;
                }
            }

            Type GetBaseType(Type t)
            {
                if (t == null)
                    return null;
                if (t.BaseType == typeof(Node))
                    return t.BaseType;
                return GetBaseType(t.BaseType);
            }

            return null;
        }

        public static Type GetGameBoxType(Stream stream)
        {
            using (var r = new GameBoxReader(stream))
                return GetGameBoxType(r);
        }

        public static Type GetGameBoxType(GameBoxReader reader)
        {
            var classID = ReadClassID(reader);

            if (classID.HasValue)
            {
                var modernID = classID.GetValueOrDefault();
                if (Node.Mappings.TryGetValue(classID.GetValueOrDefault(), out uint newerClassID))
                    modernID = newerClassID;

                Debug.WriteLine("GetGameBoxType: " + modernID.ToString("x8"));

                var availableClass = Assembly.GetExecutingAssembly().GetTypes().Where(x => x.IsClass
                        && x.Namespace?.StartsWith("GBX.NET.Engines") == true && GetBaseType(x) == typeof(Node)
                        && x.GetCustomAttribute<NodeAttribute>().ID == modernID).FirstOrDefault();

                if (availableClass == null) return null;

                return typeof(GameBox<>).MakeGenericType(availableClass);
            }

            Type GetBaseType(Type t)
            {
                if (t == null)
                    return null;
                if (t.BaseType == typeof(Node))
                    return t.BaseType;
                return GetBaseType(t.BaseType);
            }

            return null;
        }
    }
}
