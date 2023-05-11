namespace GBX.NET.Engines.Game;

/// <summary>
/// Item placed on a map.
/// </summary>
/// <remarks>ID: 0x03101000</remarks>
[Node(0x03101000)]
public partial class CGameCtnAnchoredObject : CMwNod
{
    public enum EPhaseOffset
    {
        None,
        One8th,
        Two8th,
        Three8th,
        Four8th,
        Five8th,
        Six8th,
        Seven8th
    }

    /// <summary>
    /// Variant index of the item. Taken from flags.
    /// </summary>
    [NodeMember]
    [AppliedWithChunk<Chunk03101002>(sinceVersion: 4)]
    public int Variant
    {
        get => (flags >> 8) & 15;
        set => flags = (short)((flags & 0xF0FF) | ((value & 15) << 8));
    }

    /// <summary>
    /// Block that tells when that block gets deleted, this item is deleted with it. Works for TM2020 only.
    /// </summary>
    [NodeMember]
    public CGameCtnBlock? SnappedOnBlock { get; set; }

    /// <summary>
    /// Item that tells when that item gets deleted, this item is deleted with it. Works for TM2020 only but is more modern.
    /// </summary>
    [NodeMember]
    public CGameCtnAnchoredObject? SnappedOnItem { get; set; }

    /// <summary>
    /// Item that tells when that item gets deleted, this item is deleted with it. Works for ManiaPlanet, used to work in the past in TM2020 but now it likely doesn't.
    /// </summary>
    [NodeMember]
    public CGameCtnAnchoredObject? PlacedOnItem { get; set; }

    /// <summary>
    /// Group number that groups items that get deleted together. Works for TM2020 only.
    /// </summary>
    [NodeMember]
    public int? SnappedOnGroup { get; set; }

    /// <summary>
    /// Color of the item. Available since TM2020 Royal update.
    /// </summary>
    [NodeMember(ExactName = "MapElemColor")]
    public DifficultyColor? Color { get; set; }

    /// <summary>
    /// Phase of the animation. Available since TM2020 Royal update.
    /// </summary>
    [NodeMember]
    public EPhaseOffset? AnimPhaseOffset { get; set; }

    /// <summary>
    /// The second layer of skin. Available since TM2020.
    /// </summary>
    [NodeMember]
    public FileRef? ForegroundPackDesc { get; set; }

    /// <summary>
    /// Lightmap quality setting of the item. Available since TM2020.
    /// </summary>
    [NodeMember(ExactName = "MapElemLmQuality")]
    public LightmapQuality? LightmapQuality { get; set; }

    /// <summary>
    /// Reference to the macroblock that placed this item. In macroblock mode, this item is then part of a selection group. Available since TM2020.
    /// </summary>
    [NodeMember]
    public MacroblockInstance? MacroblockReference { get; set; }

    internal CGameCtnAnchoredObject()
    {
        itemModel = Ident.Empty;
        anchorTreeId = string.Empty;
    }

    internal CGameCtnAnchoredObject(Ident itemModel, Vec3 absolutePositionInMap, Vec3 pitchYawRoll,
        Vec3 pivotPosition = default, int variant = 0, float scale = 1, Byte3 blockUnitCoord = default) : this()
    {
        this.itemModel = itemModel;
        this.absolutePositionInMap = absolutePositionInMap;
        this.pitchYawRoll = pitchYawRoll;
        this.pivotPosition = pivotPosition;
        Variant = variant;
        this.scale = scale;
        this.blockUnitCoord = blockUnitCoord;
    }

    public override string ToString()
    {
        return $"{base.ToString()} {{ {ItemModel} }}";
    }
}
