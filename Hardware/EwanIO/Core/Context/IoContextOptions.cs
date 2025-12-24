namespace EwanIO.Core.Context
{
    public enum IndexOutOfRangeBehavior
    {
        Ignore = 0,
        RecordError = 1,
        Throw = 2
    }

    public sealed class IoContextOptions
    {
        public IndexOutOfRangeBehavior IndexOutOfRangeBehavior { get; set; } = IndexOutOfRangeBehavior.RecordError;
    }
}
