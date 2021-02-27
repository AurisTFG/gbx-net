﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

namespace GBX.NET
{
    public class Node
    {
        public static Dictionary<uint, string> Names { get; }
        public static Dictionary<uint, uint> Mappings { get; } // key: older, value: newer

        [IgnoreDataMember]
        public IGameBoxBody Body { get; set; }
        [IgnoreDataMember]
        public GameBox GBX => Body?.GBX;

        public ChunkSet Chunks { get; internal set; } = new ChunkSet();

        /// <summary>
        /// Chunk where the aux node appeared
        /// </summary>
        public Chunk ParentChunk { get; set; }

        public uint ID { get; }
        public uint? FaultyChunk { get; private set; }
        [Obsolete]
        public byte[] Rest { get; private set; }
        public bool Unknown { get; internal set; }

        public uint ModernID
        {
            get
            {
                if (Mappings.TryGetValue(ID, out uint newerClassID))
                    return newerClassID;
                return ID;
            }
        }

        public string ClassName
        {
            get
            {
                if (Names.TryGetValue(ModernID, out string name))
                    return name;
                return GetType().FullName.Substring("GBX.NET.Engines".Length + 1).Replace(".", "::");
            }
        }

        [IgnoreDataMember]
        public uint[] ChunkIDs
        {
            get
            {
                var chunkTypes = GetType().GetNestedTypes().Where(x => x.BaseType == typeof(Chunk)).ToArray();
                var array = new uint[chunkTypes.Length];

                for (var i = 0; i < array.Length; i++)
                {
                    var att = chunkTypes[i].GetCustomAttribute<ChunkAttribute>();
                    array[i] = att.ClassID + att.ChunkID;
                }

                return array;
            }
        }

        public static Dictionary<uint, Type> AvailableClasses { get; } = new Dictionary<uint, Type>();
        public static Dictionary<Type, List<uint>> AvailableInheritanceClasses { get; } = new Dictionary<Type, List<uint>>();
        public static Dictionary<Type, Dictionary<uint, Type>> AvailableChunkClasses { get; } = new Dictionary<Type, Dictionary<uint, Type>>();
        public static Dictionary<Type, Dictionary<uint, Type>> AvailableHeaderChunkClasses { get; } = new Dictionary<Type, Dictionary<uint, Type>>();

        public Node()
        {
            ID = GetType().GetCustomAttribute<NodeAttribute>().ID;
        }

        public Node(uint classID)
        {
            ID = classID;
        }

        public Node(params Chunk[] chunks) : this()
        {
            foreach (var chunk in chunks)
            {
                GetType()
                    .GetMethod("CreateChunk")
                    .MakeGenericMethod(chunk.GetType())
                    .Invoke(this, new object[0]);
            }
        }

        public static T[] ParseArray<T>(GameBoxReader r) where T : Node
        {
            var count = r.ReadInt32();
            var array = new T[count];

            for (var i = 0; i < count; i++)
                array[i] = Parse<T>(r);

            return array;
        }

        public static T Parse<T>(GameBoxReader r, uint? classID = null, GameBox<T> gbx = null, IProgress<GameBoxReadProgress> progress = null) where T : Node
        {
            var stopwatch = Stopwatch.StartNew();

            if (!classID.HasValue)
                classID = r.ReadUInt32();

            if (classID == uint.MaxValue) return null;

            classID = Remap(classID.Value);

            if (!AvailableClasses.TryGetValue(classID.Value, out Type type))
                throw new NotImplementedException($"Node ID 0x{classID.Value:X8} is not implemented. ({Names.Where(x => x.Key == Chunk.Remap(classID.Value)).Select(x => x.Value).FirstOrDefault() ?? "unknown class"})");

            T node = null;
            if (gbx == null)
                node = (T)Activator.CreateInstance(type);
            else
                node = gbx.MainNode;

            IGameBoxBody body;

            if (r.Lookbackable is Chunk ch)
                body = (IGameBoxBody)ch.Part;
            else
                body = (IGameBoxBody)r.Lookbackable;

            node.Body = body;

            var chunks = new ChunkSet { Node = node };
            node.Chunks = chunks;

            uint? previousChunk = null;

            while (r.BaseStream.Position < r.BaseStream.Length)
            {
                if (r.BaseStream.Position + 4 > r.BaseStream.Length)
                {
                    Debug.WriteLine($"Unexpected end of the stream: {r.BaseStream.Position}/{r.BaseStream.Length}");
                    var bytes = r.ReadBytes((int)(r.BaseStream.Length - r.BaseStream.Position));
                    break;
                }

                var chunkID = r.ReadUInt32();

                if (chunkID == 0xFACADE01) // no more chunks
                {
                    break;
                }
                else if (chunkID == 0)
                {
                    // weird case after ending node reference
                }
                else
                {
                    var logChunk = $"[{node.ClassName}] 0x{chunkID:X8} ({(float)r.BaseStream.Position / r.BaseStream.Length:0.00%})";
                    if (node.Body?.GBX.ClassID.HasValue == true && Remap(node.Body.GBX.ClassID.Value) == node.ID)
                        Log.Write(logChunk);
                    else
                        Log.Write($"~ {logChunk}");
                }

                Type chunkClass = null;
                Chunk chunk = null;

                var chunkRemapped = Chunk.Remap(chunkID);

                var reflected = ((chunkRemapped & 0xFFFFF000) == node.ID || AvailableInheritanceClasses[type].Contains(chunkRemapped & 0xFFFFF000))
                    && (AvailableChunkClasses[type].TryGetValue(chunkRemapped, out chunkClass) || AvailableChunkClasses[type].TryGetValue(chunkID & 0xFFF, out chunkClass));

                var skippable = reflected && chunkClass.BaseType.GetGenericTypeDefinition() == typeof(SkippableChunk<>);
                
                if (!reflected || skippable)
                {
                    var skip = r.ReadUInt32();

                    if (skip != 0x534B4950)
                    {
                        if (chunkID != 0 && !reflected)
                        {
                            node.FaultyChunk = chunkID;

                            var logChunkError = $"[{node.ClassName}] 0x{chunkID:X8} ERROR (wrong chunk format or unknown unskippable chunk)";
                            if (node.Body?.GBX.ClassID.HasValue == true && Remap(node.Body.GBX.ClassID.Value) == node.ID)
                                Log.Write(logChunkError, ConsoleColor.Red);
                            else
                                Log.Write($"~ {logChunkError}", ConsoleColor.Red);

                            throw new Exception($"Wrong chunk format or unskippable chunk: 0x{chunkID:X8} (" +
                                $"{Names.Where(x => x.Key == Chunk.Remap(chunkID & 0xFFFFF000)).Select(x => x.Value).FirstOrDefault() ?? "unknown class"})" +
                                $"\nPrevious chunk: 0x{previousChunk ?? 0:X8} (" +
                                $"{(previousChunk.HasValue ? (Names.Where(x => x.Key == Chunk.Remap(previousChunk.Value & 0xFFFFF000)).Select(x => x.Value).FirstOrDefault() ?? "unknown class") : "not a class")})");

                            /* Usually breaks in the current state and causes confusion
                             * 
                             * var buffer = BitConverter.GetBytes(chunkID);
                            using (var restMs = new MemoryStream(ushort.MaxValue))
                            {
                                restMs.Write(buffer, 0, buffer.Length);

                                while (r.PeekUInt32() != 0xFACADE01)
                                    restMs.WriteByte(r.ReadByte());

                                node.Rest = restMs.ToArray();
                            }
                            Debug.WriteLine("FACADE found.");*/
                        }
                        break;
                    }

                    var chunkDataSize = r.ReadInt32();
                    var chunkData = new byte[chunkDataSize];
                    if (chunkDataSize > 0)
                        r.Read(chunkData, 0, chunkDataSize);

                    if (reflected && chunkClass.GetCustomAttribute<IgnoreChunkAttribute>() == null)
                    {
                        ISkippableChunk c;

                        var constructor = chunkClass.GetConstructors().First();
                        var constructorParams = constructor.GetParameters();
                        if (constructorParams.Length == 0)
                        {
                            c = (ISkippableChunk)constructor.Invoke(new object[0]);
                            c.Node = node;
                            c.Part = (GameBoxPart)body;
                            c.Stream = new MemoryStream(chunkData, 0, chunkData.Length, false);
                            if (chunkData == null || chunkData.Length == 0)
                                c.Discovered = true;
                            c.OnLoad();
                        }
                        else if (constructorParams.Length == 2)
                            c = (ISkippableChunk)constructor.Invoke(new object[] { node, chunkData });
                        else throw new ArgumentException($"{type.FullName} has an invalid amount of parameters.");

                        chunks.Add((Chunk)c);

                        if (chunkClass.GetCustomAttribute<ChunkAttribute>().ProcessSync)
                            c.Discover();

                        chunk = (Chunk)c;
                    }
                    else
                    {
                        Debug.WriteLine("Unknown skippable chunk: " + chunkID.ToString("X"));
                        chunk = (Chunk)Activator.CreateInstance(typeof(SkippableChunk<>).MakeGenericType(type), node, chunkID, chunkData);
                        chunks.Add(chunk);
                    }
                }

                if (reflected && !skippable)
                {
                    if (skippable) // Does it ever happen?
                    {
                        var skip = r.ReadUInt32();
                        var chunkDataSize = r.ReadInt32();
                    }

                    IChunk c;

                    var constructor = chunkClass.GetConstructors().First();
                    var constructorParams = constructor.GetParameters();
                    if (constructorParams.Length == 0)
                    {
                        c = (IChunk)constructor.Invoke(new object[0]);
                        c.Node = node;
                    }
                    else if (constructorParams.Length == 1)
                        c = (IChunk)constructor.Invoke(new object[] { node });
                    else throw new ArgumentException($"{type.FullName} has an invalid amount of parameters.");

                    c.Part = (GameBoxPart)body;
                    c.OnLoad();

                    chunks.Add((Chunk)c);

                    r.Chunk = (Chunk)c; // Set chunk temporarily for reading

                    var posBefore = r.BaseStream.Position;

                    GameBoxReaderWriter gbxrw = new GameBoxReaderWriter(r);

                    try
                    {
                        if (chunkClass.GetCustomAttribute<IgnoreChunkAttribute>() == null)
                        {
                            if(chunkClass.GetCustomAttribute<AutoReadWriteChunkAttribute>() == null)
                                c.ReadWrite(node, gbxrw);
                            else
                            {
                                var unknown = new GameBoxWriter(((Chunk)c).Unknown, r.Lookbackable);
                                var unknownData = r.ReadTillFacade();
                                unknown.Write(unknownData, 0, unknownData.Length);
                            }
                        }
                        else
                            throw new Exception($"Chunk 0x{chunkID & 0xFFF:x3} from class {node.ClassName} is known but its content is unknown to read.");
                    }
                    catch (EndOfStreamException)
                    {
                        Debug.WriteLine($"Unexpected end of the stream while reading the chunk.");
                    }

                    c.Progress = (int)(r.BaseStream.Position - posBefore);

                    r.Chunk = null;

                    chunk = (Chunk)c;
                }

                progress?.Report(new GameBoxReadProgress(GameBoxReadProgressStage.Body, (float)r.BaseStream.Position / r.BaseStream.Length, gbx, chunk));

                previousChunk = chunkID;
            }

            stopwatch.Stop();

            var logNodeCompletion = $"[{node.ClassName}] DONE! ({stopwatch.Elapsed.TotalMilliseconds}ms)";
            if (node.Body?.GBX.ClassID.HasValue == true && Remap(node.Body.GBX.ClassID.Value) == node.ID)
                Log.Write(logNodeCompletion, ConsoleColor.Green);
            else
                Log.Write($"~ {logNodeCompletion}", ConsoleColor.Green);

            return node;
        }

        public void Read(GameBoxReader r)
        {
            throw new NotImplementedException($"Node doesn't support Read.");
        }

        public void Write(GameBoxWriter w)
        {
            Write(w, ClassIDRemap.Latest);
        }

        public void Write(GameBoxWriter w, ClassIDRemap remap)
        {
            var stopwatch = Stopwatch.StartNew();

            int counter = 0;

            foreach (Chunk chunk in Chunks)
            {
                counter++;

                var logChunk = $"[{ClassName}] 0x{chunk.ID:X8} ({(float)counter / Chunks.Count:0.00%})";
                if (Body?.GBX.ClassID.HasValue == true && Remap(Body.GBX.ClassID.Value) == ID)
                    Log.Write(logChunk);
                else
                    Log.Write($"~ {logChunk}");

                ((IChunk)chunk).Node = this;
                chunk.Unknown.Position = 0;

                ILookbackable lb = chunk.Lookbackable;

                if (chunk is ILookbackable l)
                {
                    l.IdWritten = false;
                    l.IdStrings.Clear();

                    lb = l;
                }

                if (lb == null)
                {
                    if(ParentChunk is ILookbackable l2)
                        lb = l2;
                    else
                        lb = w.Lookbackable;
                }

                using (var ms = new MemoryStream())
                using (var msW = new GameBoxWriter(ms, lb))
                {
                    var rw = new GameBoxReaderWriter(msW);

                    msW.Chunk = chunk;

                    try
                    {
                        if (chunk is ISkippableChunk s && !s.Discovered)
                            s.Write(msW);
                        else if (chunk.GetType().GetCustomAttribute<AutoReadWriteChunkAttribute>() == null)
                            ((IChunk)chunk).ReadWrite(this, rw);
                        else
                            msW.Write(chunk.Unknown.ToArray(), 0, (int)chunk.Unknown.Length);

                        w.Write(Chunk.Remap(chunk.ID, remap));

                        if (chunk is ISkippableChunk)
                        {
                            w.Write(0x534B4950);
                            w.Write((int)ms.Length);
                        }

                        w.Write(ms.ToArray(), 0, (int)ms.Length);
                    }
                    catch (NotImplementedException e)
                    {
                        if (chunk is ISkippableChunk)
                        {
                            Debug.WriteLine(e.Message);
                            Debug.WriteLine("Ignoring the skippable chunk from writing.");
                        }
                        else throw e; // Unskippable chunk must have a Write implementation
                    }

                    msW.Chunk = null;
                }
            }

            w.Write(0xFACADE01);

            stopwatch.Stop();

            var logNodeCompletion = $"[{ClassName}] DONE! ({stopwatch.Elapsed.TotalMilliseconds}ms)";
            if (Body?.GBX.ClassID.HasValue == true && Remap(Body.GBX.ClassID.Value) == ID)
                Log.Write(logNodeCompletion, ConsoleColor.Green);
            else
                Log.Write($"~ {logNodeCompletion}", ConsoleColor.Green);
        }

        static Node()
        {
            Names = new Dictionary<uint, string>();
            Mappings = new Dictionary<uint, uint>();

            var startTimestamp = DateTime.Now;

            using (StringReader reader = new StringReader(Resources.ClassID))
            {
                var en = "";
                var engineName = "";

                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    var ch = "000";

                    var className = "";

                    if (line.StartsWith("  "))
                    {
                        var cl = line.Substring(2, 3);
                        if (line.Length - 6 > 0) className = line.Substring(6);

                        var classIDString = $"{en}{cl}{ch}";

                        if (uint.TryParse(classIDString, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out uint classID))
                        {
                            Names[classID] = engineName + "::" + className;
                        }
                        else
                        {
                            Debug.WriteLine($"Invalid class ID {classIDString}, skipping");
                        }
                    }
                    else
                    {
                        en = line.Substring(0, 2);
                        if (line.Length - 3 > 0) engineName = line.Substring(3);
                    }
                }
            }

            Debug.WriteLine("Classes named in " + (DateTime.Now - startTimestamp).TotalMilliseconds + "ms");

            startTimestamp = DateTime.Now;

            using (StringReader reader = new StringReader(Resources.ClassIDMappings))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    var valueKey = line.Split(new string[] { " -> " }, StringSplitOptions.None);
                    if (valueKey.Length == 2)
                    {
                        if (uint.TryParse(valueKey[0], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out uint key)
                        && uint.TryParse(valueKey[1], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out uint value))
                        {
                            if (Mappings.ContainsValue(key)) // Virtual Skipper solution
                                Mappings[Mappings.FirstOrDefault(x => x.Value == key).Key] = value;
                            Mappings[key] = value;
                        }
                    }
                }
            }

            Debug.WriteLine("Mappings defined in " + (DateTime.Now - startTimestamp).TotalMilliseconds + "ms");

            startTimestamp = DateTime.Now;

            var assemb = Assembly.GetExecutingAssembly();

            IEnumerable<Type> types;
            try
            {
                types = assemb.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                types = e.Types;
            }
            types = types.Where(t => t != null);

            foreach (var nodeType in types.Where(x => x.IsClass
                   && x.Namespace != null && x.Namespace.StartsWith("GBX.NET.Engines") && x.IsSubclassOf(typeof(Node))))
            {
                var nodeID = nodeType.GetCustomAttribute<NodeAttribute>().ID;

                AvailableClasses.Add(nodeID, nodeType);

                var inheritanceClasses = GetInheritance(nodeType);
                AvailableInheritanceClasses[nodeType] = inheritanceClasses;

                List<uint> GetInheritance(Type t)
                {
                    List<uint> classes = new List<uint>();

                    Type cur = t.BaseType;

                    while (cur != typeof(Node))
                    {
                        classes.Add(cur.GetCustomAttribute<NodeAttribute>().ID);
                        cur = cur.BaseType;
                    }

                    return classes;
                }

                var chunkType = typeof(Chunk<>).MakeGenericType(nodeType);
                var skippableChunkType = typeof(SkippableChunk<>).MakeGenericType(nodeType);
                var headerChunkType = typeof(HeaderChunk<>).MakeGenericType(nodeType);

                var availableHeaderChunkClasses = new Dictionary<uint, Type>();

                if (!AvailableChunkClasses.TryGetValue(nodeType, out Dictionary<uint, Type> availableChunkClasses))
                {
                    availableChunkClasses = nodeType.GetNestedTypes().Where(x =>
                    {
                        var isChunk = x.IsClass
                        && x.Namespace.StartsWith("GBX.NET.Engines")
                        && (x.BaseType == chunkType || x.BaseType == skippableChunkType || x.BaseType == headerChunkType);
                        if (!isChunk) return false;

                        var chunkAttribute = x.GetCustomAttribute<ChunkAttribute>();
                        if (chunkAttribute == null) throw new Exception($"Chunk {x.FullName} doesn't have a ChunkAttribute.");

                        if (chunkAttribute.ClassID == nodeID)
                        {
                            if (x.BaseType == headerChunkType)
                            {
                                availableHeaderChunkClasses.Add(chunkAttribute.ID, x);
                                return false;
                            }

                            return true;
                        }
                        return false;
                    }).ToDictionary(x => x.GetCustomAttribute<ChunkAttribute>().ID);

                    foreach (var cls in inheritanceClasses)
                    {
                        var availableInheritanceClass = types.Where(x => x.IsClass && x.Namespace != null
                           && x.Namespace.StartsWith("GBX.NET.Engines") && x.IsSubclassOf(typeof(Node))
                           && (x.GetCustomAttribute<NodeAttribute>().ID == cls)).FirstOrDefault();

                        var inheritChunkType = typeof(Chunk<>).MakeGenericType(availableInheritanceClass);
                        var inheritSkippableChunkType = typeof(SkippableChunk<>).MakeGenericType(availableInheritanceClass);
                        var inheritHeaderChunkType = typeof(HeaderChunk<>).MakeGenericType(availableInheritanceClass);

                        foreach (var chunkT in availableInheritanceClass.GetNestedTypes().Where(x => x.IsClass
                            && x.Namespace.StartsWith("GBX.NET.Engines") && (x.BaseType == inheritChunkType || x.BaseType == inheritSkippableChunkType || x.BaseType == inheritHeaderChunkType)
                            && (x.GetCustomAttribute<ChunkAttribute>().ClassID == cls)).ToDictionary(x => x.GetCustomAttribute<ChunkAttribute>().ID + (x.BaseType == inheritHeaderChunkType ? "H" : "B")))
                        {
                            if(chunkT.Key.EndsWith("H"))
                                availableHeaderChunkClasses[uint.Parse(chunkT.Key.Remove(chunkT.Key.Length - 1))] = chunkT.Value;
                            else
                                availableChunkClasses[uint.Parse(chunkT.Key.Remove(chunkT.Key.Length - 1))] = chunkT.Value;
                        }
                    }
                    AvailableChunkClasses.Add(nodeType, availableChunkClasses);
                    AvailableHeaderChunkClasses.Add(nodeType, availableHeaderChunkClasses);
                }
            }

            Debug.WriteLine("Types defined in " + (DateTime.Now - startTimestamp).TotalMilliseconds + "ms");
        }

        public T GetChunk<T>() where T : Chunk
        {
            return Chunks.Get<T>();
        }

        public T CreateChunk<T>() where T : Chunk
        {
            return Chunks.Create<T>();
        }

        public bool RemoveChunk<T>() where T : Chunk
        {
            return Chunks.Remove<T>();
        }

        public bool RemoveChunk(uint chunkID)
        {
            return Chunks.Remove(chunkID);
        }

        public bool TryGetChunk<T>(out T chunk) where T : Chunk
        {
            return Chunks.TryGet(out chunk);
        }

        public void DiscoverChunk<T>() where T : ISkippableChunk
        {
            Chunks.Discover<T>();
        }

        public void DiscoverChunks<T1, T2>() where T1 : ISkippableChunk where T2 : ISkippableChunk
        {
            Chunks.Discover<T1, T2>();
        }

        public void DiscoverChunks<T1, T2, T3>() where T1 : ISkippableChunk where T2 : ISkippableChunk where T3 : ISkippableChunk
        {
            Chunks.Discover<T1, T2, T3>();
        }

        public void DiscoverChunks<T1, T2, T3, T4>()
            where T1 : ISkippableChunk
            where T2 : ISkippableChunk
            where T3 : ISkippableChunk
            where T4 : ISkippableChunk
        {
            Chunks.Discover<T1, T2, T3, T4>();
        }

        public void DiscoverChunks<T1, T2, T3, T4, T5>()
            where T1 : ISkippableChunk
            where T2 : ISkippableChunk
            where T3 : ISkippableChunk
            where T4 : ISkippableChunk
            where T5 : ISkippableChunk
        {
            Chunks.Discover<T1, T2, T3, T4, T5>();
        }

        public void DiscoverChunks<T1, T2, T3, T4, T5, T6>()
            where T1 : ISkippableChunk
            where T2 : ISkippableChunk
            where T3 : ISkippableChunk
            where T4 : ISkippableChunk
            where T5 : ISkippableChunk
            where T6 : ISkippableChunk
        {
            Chunks.Discover<T1, T2, T3, T4, T5, T6>();
        }

        /// <summary>
        /// Discovers all chunks in the node.
        /// </summary>
        /// <exception cref="AggregateException"/>
        public void DiscoverAllChunks()
        {
            Chunks.DiscoverAll();

            foreach (var nodeProperty in GetType().GetProperties())
            {
                if (nodeProperty.PropertyType.IsSubclassOf(typeof(Node)))
                {
                    var node = (Node)nodeProperty.GetValue(this);
                    node?.DiscoverAllChunks();
                }
            }
        }

        /// <summary>
        /// Makes a <see cref="GameBox"/> from this node. NOTE: Non-generic <see cref="GameBox"/> doesn't have a Save method.
        /// </summary>
        /// <param name="headerInfo"></param>
        /// <returns></returns>
        public GameBox ToGBX(GameBoxHeaderInfo headerInfo)
        {
            return (GameBox)Activator.CreateInstance(typeof(GameBox<>).MakeGenericType(GetType()), this, headerInfo);
        }

        /// <summary>
        /// Makes a <see cref="GameBox"/> from this node. NOTE: Non-generic <see cref="GameBox"/> doesn't have a Save method. You can explicitly cast it to <see cref="GameBox{T}"/> depending on the <see cref="Node"/>.
        /// </summary>
        /// <returns></returns>
        public GameBox ToGBX()
        {
            return (GameBox)Activator.CreateInstance(typeof(GameBox<>).MakeGenericType(GetType()), this);
        }

        public static uint Remap(uint id)
        {
            if (Mappings.TryGetValue(id, out uint newerClassID))
                return newerClassID;
            return id;
        }
    }
}
