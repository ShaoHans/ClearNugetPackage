using System;
using System.Collections.Generic;

public class PackageIdentityComparer : IEqualityComparer<(string Id, string Version)>
{
    public bool Equals((string Id, string Version) x, (string Id, string Version) y)
    {
        return string.Equals(x.Id, y.Id, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Version, y.Version, StringComparison.OrdinalIgnoreCase);
    }

    public int GetHashCode((string Id, string Version) obj)
    {
        // 使用 StringComparer.OrdinalIgnoreCase 的哈希码，然后合并
        int h1 = obj.Id != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Id) : 0;
        int h2 = obj.Version != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Version) : 0;
        unchecked
        {
            return (h1 * 397) ^ h2;
        }
        // 或者： return HashCode.Combine(h1, h2);
    }
}
