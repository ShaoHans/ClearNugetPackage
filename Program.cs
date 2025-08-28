using System.Xml.Linq;

string workDir = "C:\\work\\demo";
string nugetCache =
    Environment.GetEnvironmentVariable("NUGET_PACKAGES")
    ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".nuget",
        "packages"
    );

Console.WriteLine($"工作目录: {workDir}");
Console.WriteLine($"NuGet缓存目录: {nugetCache}");

// 1. 扫描项目引用
var usedPackages = new HashSet<(string Id, string Version)>(new PackageIdentityComparer());

foreach (var file in Directory.EnumerateFiles(workDir, "*.csproj", SearchOption.AllDirectories))
{
    var doc = XDocument.Load(file);
    var refs = doc.Descendants("PackageReference")
        .Select(x =>
            (
                Id: (string)x.Attribute("Include"),
                Version: (string)x.Attribute("Version") ?? (string)x.Element("Version")
            )
        )
        .Where(x => !string.IsNullOrEmpty(x.Id) && !string.IsNullOrEmpty(x.Version));

    foreach (var r in refs)
        usedPackages.Add(r);
}

foreach (
    var file in Directory.EnumerateFiles(workDir, "packages.config", SearchOption.AllDirectories)
)
{
    var doc = XDocument.Load(file);
    var refs = doc.Descendants("package")
        .Select(x => (Id: (string)x.Attribute("id"), Version: (string)x.Attribute("version")))
        .Where(x => !string.IsNullOrEmpty(x.Id) && !string.IsNullOrEmpty(x.Version));

    foreach (var r in refs)
        usedPackages.Add(r);
}

Console.WriteLine($"发现引用包: {usedPackages.Count} 个");

// 2. 遍历NuGet缓存目录
var unusedDirs = new List<string>();
foreach (
    var pkgDir in Directory
        .EnumerateDirectories(nugetCache)
        .Where(d => !Path.GetFileName(d).Equals(".tools", StringComparison.OrdinalIgnoreCase))
)
{
    string packageId = Path.GetFileName(pkgDir);
    foreach (var verDir in Directory.EnumerateDirectories(pkgDir))
    {
        string version = Path.GetFileName(verDir);
        if (!usedPackages.Contains((packageId, version)))
        {
            unusedDirs.Add(verDir);
        }
    }
}

Console.WriteLine($"待删除未使用版本: {unusedDirs.Count} 个");
foreach (var d in unusedDirs.Take(20))
    Console.WriteLine($"  {d}");
if (unusedDirs.Count > 20)
    Console.WriteLine("  ...");

Console.Write("确认删除这些目录吗？(y/n): ");
if (Console.ReadLine()?.ToLower() == "y")
{
    foreach (var d in unusedDirs)
    {
        try
        {
            Directory.Delete(d, true);
            Console.WriteLine($"已删除: {d}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"删除失败: {d}, {ex.Message}");
        }
    }
}
else
{
    Console.WriteLine("取消操作。");
}
