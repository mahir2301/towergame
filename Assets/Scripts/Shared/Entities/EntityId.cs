using System;

namespace Shared.Entities
{
    [Serializable]
    public readonly struct EntityId : IEquatable<EntityId>
    {
        public const uint InvalidValue = 0;

        public EntityId(uint value)
        {
            Value = value;
        }

        public uint Value { get; }
        public bool IsValid => Value != InvalidValue;

        public static EntityId Invalid => new(InvalidValue);

        public bool Equals(EntityId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is EntityId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (int)Value;
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public static bool operator ==(EntityId left, EntityId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(EntityId left, EntityId right)
        {
            return !left.Equals(right);
        }
    }
}
