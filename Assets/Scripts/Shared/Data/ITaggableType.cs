using System.Collections.Generic;

namespace Shared.Data
{
    public interface ITaggableType
    {
        IReadOnlyList<TagType> Tags { get; }
        bool HasTag(TagType tag);
    }
}
