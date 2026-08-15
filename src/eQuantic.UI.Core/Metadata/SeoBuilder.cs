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

    /// <summary>
    /// This page in another language — one <c>rel="alternate"</c> per translation, the set search
    /// engines read as "these URLs are the same page".
    /// <para>
    /// Three rules the standard imposes and this does not enforce for you, because only the app
    /// knows its own URLs: the set must be RECIPROCAL (every page in the group lists the same
    /// alternates), it must include the page ITSELF, and the URLs must be absolute. A page that
    /// lists its translations but not itself is a group a crawler discards whole.
    /// </para>
    /// <para>
    /// <paramref name="hreflang"/> is a BCP-47 name (<c>pt-BR</c>, <c>es</c>) or the literal
    /// <c>x-default</c>, which names where a visitor whose language matched nothing should land.
    /// Prefer <see cref="AlternateDefault"/> for that one, so the spelling cannot drift.
    /// </para>
    /// </summary>
    public SeoBuilder Alternate(string hreflang, string url)
    {
        _collection.AddOrUpdate(new LinkTag("alternate", url, hreflang: hreflang));
        return this;
    }

    /// <summary>Where a visitor whose language matches none of the alternates should land —
    /// <c>hreflang="x-default"</c>, usually the same URL as the default culture's.</summary>
    public SeoBuilder AlternateDefault(string url) => Alternate("x-default", url);

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
