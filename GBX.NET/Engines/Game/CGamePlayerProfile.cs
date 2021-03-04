﻿using GBX.NET.Engines.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GBX.NET.Engines.Game
{
    [Node(0x0308C000)]
    public class CGamePlayerProfile : Node
    {
        private string description;

        public string OnlineLogin { get; set; }
        public string OnlineSupportKey { get; set; }
        public CInputBindingsConfig InputBindings { get; set; }

        public string Description
        {
            get
            {
                DiscoverChunk<Chunk0308C029>();
                return description;
            }
            set
            {
                DiscoverChunk<Chunk0308C029>();
                description = value;
            }
        }

        #region Chunks

        #region 0x000 chunk

        [Chunk(0x0308C000)]
        public class Chunk0308C000 : Chunk<CGamePlayerProfile>
        {
            public override void ReadWrite(CGamePlayerProfile n, GameBoxReaderWriter rw)
            {
                n.OnlineLogin = rw.String(n.OnlineLogin);
                n.OnlineSupportKey = rw.String(n.OnlineSupportKey);
            }
        }

        #endregion

        #region 0x006 chunk

        [Chunk(0x0308C006)]
        public class Chunk0308C006 : Chunk<CGamePlayerProfile>
        {
            public override void ReadWrite(CGamePlayerProfile n, GameBoxReaderWriter rw)
            {
                n.InputBindings = rw.Reader.ReadNodeRef<CInputBindingsConfig>();
            }
        }

        #endregion

        #region 0x008 chunk

        [Chunk(0x0308C008)]
        public class Chunk0308C008 : Chunk<CGamePlayerProfile>
        {
            public override void ReadWrite(CGamePlayerProfile n, GameBoxReaderWriter rw)
            {
                rw.Int32(Unknown);
            }
        }

        #endregion

        #region 0x009 chunk

        [Chunk(0x0308C009)]
        public class Chunk0308C009 : Chunk<CGamePlayerProfile>
        {
            public override void ReadWrite(CGamePlayerProfile n, GameBoxReaderWriter rw)
            {
                rw.Int32(Unknown);
                rw.Int32(Unknown);
                rw.Int32(Unknown);
                rw.Int32(Unknown);
                rw.Int32(Unknown);
                rw.Int32(Unknown);
            }
        }

        #endregion

        #region 0x00A chunk

        [Chunk(0x0308C00A)]
        public class Chunk0308C00A : Chunk<CGamePlayerProfile>
        {
            public override void ReadWrite(CGamePlayerProfile n, GameBoxReaderWriter rw)
            {
                rw.Int32(Unknown);
                rw.Int32(Unknown);
            }
        }

        #endregion

        #region 0x011 chunk

        [Chunk(0x0308C011)]
        public class Chunk0308C011 : Chunk<CGamePlayerProfile>
        {
            public override void ReadWrite(CGamePlayerProfile n, GameBoxReaderWriter rw)
            {
                rw.Int32(Unknown);
            }
        }

        #endregion

        #region 0x014 chunk

        [Chunk(0x0308C014)]
        public class Chunk0308C014 : Chunk<CGamePlayerProfile>
        {
            public override void ReadWrite(CGamePlayerProfile n, GameBoxReaderWriter rw)
            {
                rw.Int32(Unknown);
            }
        }

        #endregion

        #region 0x01B chunk

        [Chunk(0x0308C01B)]
        public class Chunk0308C01B : Chunk<CGamePlayerProfile>
        {
            public override void ReadWrite(CGamePlayerProfile n, GameBoxReaderWriter rw)
            {
                var cars = rw.Reader.ReadArray(i => rw.Reader.ReadId());
                rw.Int32(Unknown);
                rw.Int32(Unknown);
                rw.Int32(Unknown);
                rw.Int32(Unknown);
                rw.Int32(Unknown);
            }
        }

        #endregion

        #region 0x01E chunk

        [Chunk(0x0308C01E)]
        public class Chunk0308C01E : Chunk<CGamePlayerProfile>
        {
            public override void Read(CGamePlayerProfile n, GameBoxReader r, GameBoxWriter unknownW)
            {
                var skins = r.ReadArray<object>(i =>
                {
                    return new Skin
                    {
                        PlayerModel = r.ReadIdent(),
                        SkinFile = r.ReadString(),
                        Checksum = r.ReadUInt32()
                    };
                });
            }
        }

        #endregion

        #region 0x01F chunk

        [Chunk(0x0308C01F)]
        public class Chunk0308C01F : Chunk<CGamePlayerProfile>
        {
            public override void ReadWrite(CGamePlayerProfile n, GameBoxReaderWriter rw)
            {
                var profileName = rw.Reader.ReadString();
                var displayProfileName = rw.Reader.ReadString();
                var deviceGuid = rw.Reader.ReadId();
                rw.Int32(Unknown);
            }
        }

        #endregion

        #region 0x023 chunk

        [Chunk(0x0308C023)]
        public class Chunk0308C023 : Chunk<CGamePlayerProfile>
        {
            public override void ReadWrite(CGamePlayerProfile n, GameBoxReaderWriter rw)
            {
                rw.Int32(Unknown);
            }
        }

        #endregion

        #region 0x029 chunk

        [Chunk(0x0308C029)]
        public class Chunk0308C029 : SkippableChunk<CGamePlayerProfile>
        {
            public override void ReadWrite(CGamePlayerProfile n, GameBoxReaderWriter rw)
            {
                rw.String(ref n.description);
            }
        }

        #endregion

        #region 0x07C chunk

        [Chunk(0x0308C07C)]
        public class Chunk0308C07C : SkippableChunk<CGamePlayerProfile>
        {
            public override void ReadWrite(CGamePlayerProfile n, GameBoxReaderWriter rw)
            {
                var keyboardGuid = rw.Reader.ReadId();
                var profileName = rw.Reader.ReadString();
            }
        }

        #endregion

        #endregion

        public class Skin
        {
            public Ident PlayerModel { get; set; }
            public string SkinFile { get; set; }
            public uint Checksum { get; set; }
        }
    }
}