namespace LockOverseer.Commands;

[AttributeUsage(AttributeTargets.Method)]
public sealed class RequireFlagAttribute : Attribute
{
    public string Flag { get; }
    public RequireFlagAttribute(string flag) => Flag = flag;
}
