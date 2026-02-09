namespace CachingAOP;

[AttributeUsage(AttributeTargets.Method)]
public class CacheAttribute : Attribute
{
    public int Seconds { get; set; } = 30;
    public bool Revoke { get; set; }
}