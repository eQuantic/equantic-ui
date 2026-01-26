using System;

namespace eQuantic.UI.Core.Metadata;

public class SeoBuilder
{
    private readonly MetadataCollection _collection;

    public SeoBuilder(MetadataCollection collection)
    {
        _collection = collection;
    }

    public SeoBuilder Title(string title)
    {
        _collection.Title = title;
        _collection.AddOrUpdate(new PropertyMetaTag("og:title", title));
        _collection.AddOrUpdate(new NameMetaTag("twitter:title", title));
        return this;
    }

    public SeoBuilder Description(string description)
    {
        _collection.AddOrUpdate(new NameMetaTag("description", description));
        _collection.AddOrUpdate(new PropertyMetaTag("og:description", description));
        _collection.AddOrUpdate(new NameMetaTag("twitter:description", description));
        return this;
    }

    public SeoBuilder Canonical(string url)
    {
        _collection.AddOrUpdate(new LinkTag("canonical", url));
        _collection.AddOrUpdate(new PropertyMetaTag("og:url", url));
        return this;
    }

    public SeoBuilder Image(string url, string? alt = null)
    {
        _collection.AddOrUpdate(new PropertyMetaTag("og:image", url));
        _collection.AddOrUpdate(new NameMetaTag("twitter:image", url));
        if (alt != null)
        {
            _collection.AddOrUpdate(new PropertyMetaTag("og:image:alt", alt));
            _collection.AddOrUpdate(new NameMetaTag("twitter:image:alt", alt));
        }
        return this;
    }

    public SeoBuilder Keywords(params string[] keywords)
    {
        _collection.AddOrUpdate(new NameMetaTag("keywords", string.Join(", ", keywords)));
        return this;
    }

    public SeoBuilder Robots(bool index = true, bool follow = true)
    {
        var content = $"{(index ? "index" : "noindex")}, {(follow ? "follow" : "nofollow")}";
        _collection.AddOrUpdate(new NameMetaTag("robots", content));
        return this;
    }

    public SeoBuilder OpenGraph(string property, string content)
    {
        _collection.AddOrUpdate(new PropertyMetaTag($"og:{property}", content));
        return this;
    }

    public SeoBuilder Twitter(string name, string content)
    {
        _collection.AddOrUpdate(new NameMetaTag($"twitter:{name}", content));
        return this;
    }
}
