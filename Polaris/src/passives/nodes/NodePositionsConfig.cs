using System.Collections.Generic;

namespace Polaris;

/// <summary>
/// JSON model for polarispositions.json.
/// Maps "ConstellationName:nodeCode" -> [x, y].
/// </summary>
public class NodePositionsConfig
{
    public Dictionary<string, int[]> Positions { get; set; } = [];
}
