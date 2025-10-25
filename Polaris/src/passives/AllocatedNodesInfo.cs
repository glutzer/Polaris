using System.Collections.Generic;

namespace Polaris;

public class AllocatedNodesInfo
{
    public readonly HashSet<string> AllocatedNodeCodes = [];
    private readonly Dictionary<string, int> TagCounts = [];

    public void IncrementTags(HashSet<string> tags)
    {
        foreach (string tag in tags)
        {
            if (!TagCounts.TryGetValue(tag, out int value))
            {
                TagCounts[tag] = 1;
            }
            TagCounts[tag] = ++value;
        }
    }

    public void DecrementTags(HashSet<string> tags)
    {
        foreach (string tag in tags)
        {
            if (TagCounts.TryGetValue(tag, out int value))
            {
                value--;
                if (value <= 0)
                {
                    TagCounts.Remove(tag);
                }
                else
                {
                    TagCounts[tag] = value;
                }
            }
        }
    }

    public int GetTagCount(string tag)
    {
        return TagCounts.TryGetValue(tag, out int value) ? value : 0;
    }
}