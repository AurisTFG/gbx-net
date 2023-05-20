namespace GBX.NET.Engines.GameData;

/// <summary>
/// Waypoint.
/// </summary>
public partial class CGameWaypointSpecialProperty : CMwNod
{
    private int? spawn;

    [NodeMember]
    [AppliedWithChunk<Chunk2E009000>(sinceVersion: 1, upToVersion: 1)]
    public int? Spawn { get => spawn; set => spawn = value; }

    internal CGameWaypointSpecialProperty()
    {

    }
}
