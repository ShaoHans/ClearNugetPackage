using System.Collections.Concurrent;
using System.Xml.Linq;

string workDir = "D:\\Work";
string nugetCache =
    Environment.GetEnvironmentVariable("NUGET_PACKAGES")
    ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".nuget",
        "packages"
    );

Console.WriteLine($"工作目录: {workDir}");
Console.WriteLine($"NuGet缓存目录: {nugetCache}");

var usedPackages = new ConcurrentDictionary<(string Id, string Version), byte>(new PackageIdentityComparer());

// 1. 递归一次，找到所有项目文件
var projectFiles = Directory.EnumerateFiles(workDir, "*.*", SearchOption.AllDirectories)
    .Where(f => f.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
             || Path.GetFileName(f).Equals("packages.config", StringComparison.OrdinalIgnoreCase))
    .ToList();

Console.WriteLine($"发现项目文件: {projectFiles.Count}");

// 2. 并行解析项目文件
Parallel.ForEach(projectFiles, file =>
{
    try
    {
        if (file.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            var doc = XDocument.Load(file);
            var refs = doc.Descendants("PackageReference")
                          .Select(x => (
                              Id: (string)x.Attribute("Include"),
                              Version: (string)x.Attribute("Version") ?? (string)x.Element("Version")
                          ))
                          .Where(x => !string.IsNullOrEmpty(x.Id) && !string.IsNullOrEmpty(x.Version));
            foreach (var r in refs)
                usedPackages.TryAdd(r, 0);
        }
        else if (Path.GetFileName(file).Equals("packages.config", StringComparison.OrdinalIgnoreCase))
        {
            var doc = XDocument.Load(file);
            var refs = doc.Descendants("package")
                          .Select(x => (
                              Id: (string)x.Attribute("id"),
                              Version: (string)x.Attribute("version")
                          ))
                          .Where(x => !string.IsNullOrEmpty(x.Id) && !string.IsNullOrEmpty(x.Version));
            foreach (var r in refs)
                usedPackages.TryAdd(r, 0);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"解析 {file} 失败: {ex.Message}");
    }
});

Console.WriteLine($"发现引用包: {usedPackages.Count} 个");

// 3. 并行遍历 NuGet 缓存目录，排除 .tools
var unusedDirs = new ConcurrentBag<string>();
var packageDirs = Directory.EnumerateDirectories(nugetCache)
    .Where(d => !Path.GetFileName(d).Equals(".tools", StringComparison.OrdinalIgnoreCase));

Parallel.ForEach(packageDirs, pkgDir =>
{
    string packageId = Path.GetFileName(pkgDir);
    foreach (var verDir in Directory.EnumerateDirectories(pkgDir))
    {
        string version = Path.GetFileName(verDir);
        if (!usedPackages.ContainsKey((packageId, version)))
        {
            unusedDirs.Add(verDir);
        }
    }
});

Console.WriteLine($"待删除未使用版本: {unusedDirs.Count} 个");
foreach (var d in unusedDirs.Take(20)) Console.WriteLine($"  {d}");
if (unusedDirs.Count > 20) Console.WriteLine("  ...");

if (unusedDirs.Count == 0)
{
    Console.WriteLine("没有找到可删除的目录。");
    return;
}


Console.Write("确认删除这些目录吗？(y/n): ");
if (Console.ReadLine()?.ToLower() == "y")
{
    var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };

    Parallel.ForEach(unusedDirs, options, dir =>
    {
        try
        {
            Directory.Delete(dir, true);
            Console.WriteLine($"已删除: {dir}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"删除失败: {dir}, {ex.Message}");
        }
    });

    Console.WriteLine("清理完成。");
}
else
{
    Console.WriteLine("取消操作。");
}