using System;

namespace EwanIO.Core.Attributes
{
    public readonly struct InputSignal : IEquatable<InputSignal>
    {
        public bool Value { get; }

        public InputSignal(bool value)
        {
            Value = value;
        }

        public bool Equals(InputSignal other) => Value == other.Value;

        public override bool Equals(object? obj) => obj is InputSignal other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => Value ? "ON" : "OFF";

        public static implicit operator bool(InputSignal signal) => signal.Value;
    }

    public readonly struct OutputSignal : IEquatable<OutputSignal>
    {
        public bool Value { get; }

        public OutputSignal(bool value)
        {
            Value = value;
        }

        public bool Equals(OutputSignal other) => Value == other.Value;

        public override bool Equals(object? obj) => obj is OutputSignal other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => Value ? "ON" : "OFF";

        public static implicit operator bool(OutputSignal signal) => signal.Value;
    }
}
