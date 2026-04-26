using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shared.Data
{
    [Serializable]
    public abstract class TypeRegistry<T> where T : ScriptableObject, IRegistryType
    {
        [SerializeField] private List<T> items = new();

        [NonSerialized] private Dictionary<string, T> lookup;

        public IReadOnlyList<T> Items => items;

        public void SetItems(List<T> values)
        {
            items = values ?? new List<T>();
            Invalidate();
        }

        public void Invalidate()
        {
            lookup = null;
        }

        public T Get(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            EnsureLookup();
            return lookup.TryGetValue(id, out var value) ? value : null;
        }

        public bool HasId(string id)
        {
            if (string.IsNullOrEmpty(id))
                return false;

            EnsureLookup();
            return lookup.ContainsKey(id);
        }

        public void GetWithTag(TagType tag, List<T> results)
        {
            if (results == null)
                return;

            results.Clear();
            if (tag == null)
                return;

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item is ITaggableType tagged && tagged.HasTag(tag))
                    results.Add(item);
            }
        }

        public bool Validate(out string issue)
        {
            var listName = ListName;
            var typeName = TypeName;
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null)
                {
                    issue = $"{listName} contains a null entry at index {i}.";
                    return false;
                }

                var id = item.Id;
                if (string.IsNullOrWhiteSpace(id))
                {
                    issue = $"{typeName} '{item.name}' has an empty id.";
                    return false;
                }

                if (!seenIds.Add(id))
                {
                    issue = $"Duplicate {typeName} id '{id}'.";
                    return false;
                }

                if (!ValidateTagsIfPresent(item, typeName, out issue))
                    return false;

                if (!ValidateItem(item, out issue))
                    return false;
            }

            if (!ValidateCollection(items, out issue))
                return false;

            issue = null;
            return true;
        }

        protected abstract string ListName { get; }
        protected abstract string TypeName { get; }

        protected virtual bool ValidateItem(T item, out string issue)
        {
            issue = null;
            return true;
        }

        protected virtual bool ValidateCollection(IReadOnlyList<T> allItems, out string issue)
        {
            issue = null;
            return true;
        }

        private static bool ValidateTagsIfPresent(T item, string typeName, out string issue)
        {
            if (item is not ITaggableType tagged)
            {
                issue = null;
                return true;
            }

            var tags = tagged.Tags;
            if (tags == null)
            {
                issue = null;
                return true;
            }

            var seenTags = new HashSet<TagType>();
            for (var i = 0; i < tags.Count; i++)
            {
                var tag = tags[i];
                if (tag == null)
                {
                    issue = $"{typeName} '{item.Id}' contains a null tag at index {i}.";
                    return false;
                }

                if (!seenTags.Add(tag))
                {
                    issue = $"{typeName} '{item.Id}' contains duplicate tag '{tag.Id}'.";
                    return false;
                }
            }

            issue = null;
            return true;
        }

        private void EnsureLookup()
        {
            if (lookup != null)
                return;

            lookup = new Dictionary<string, T>(items.Count, StringComparer.Ordinal);
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null)
                    continue;

                var id = item.Id;
                if (string.IsNullOrEmpty(id))
                    continue;

                if (lookup.TryGetValue(id, out var existing) && existing != item)
                    throw new InvalidOperationException($"Duplicate registry id '{id}' detected while building {typeof(T).Name} lookup.");

                lookup[id] = item;
            }
        }
    }
}
