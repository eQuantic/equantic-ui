using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace eQuantic.UI.Core.Metadata;

public class MetadataCollection
{
    private readonly ConcurrentDictionary<string, IMetaTag> _tags = new();
    
    public string? Title { get; set; }

    public void Add(IMetaTag tag)
    {
        _tags[tag.Key] = tag;
    }

    public void AddOrUpdate(IMetaTag tag)
    {
        _tags.AddOrUpdate(tag.Key, tag, (_, _) => tag);
    }

    public IEnumerable<IMetaTag> Tags => _tags.Values;

    public string RenderTags()
    {
        return string.Join("\n    ", _tags.Values.OrderBy(t => t.Key).Select(t => t.Render()));
    }
}
