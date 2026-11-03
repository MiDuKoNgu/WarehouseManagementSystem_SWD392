using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;

var root = args[0];
var dry = args.Length > 1 && args[1] == "--dry";
foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
{
    if (file.Contains("\\obj\\") || file.Contains("\\bin\\") || file.Contains("\\.codex\\")) continue;
    var source = File.ReadAllText(file);
    var tree = CSharpSyntaxTree.ParseText(source);
    var unit = tree.GetCompilationUnitRoot();
    var members = unit.Members.Count == 1 && unit.Members[0] is BaseNamespaceDeclarationSyntax ns ? ns.Members : unit.Members;
    var types = members.OfType<BaseTypeDeclarationSyntax>().Cast<MemberDeclarationSyntax>().Concat(members.OfType<DelegateDeclarationSyntax>()).OrderBy(x => x.SpanStart).ToArray();
    if (types.Length < 2 || types.Length != members.Count) continue;
    var prefix = source[..types[0].FullSpan.Start];
    var suffix = source[types[^1].FullSpan.End..];
    var plans = types.Select(t => (name: t switch { BaseTypeDeclarationSyntax b => b.Identifier.ValueText, DelegateDeclarationSyntax d => d.Identifier.ValueText, _ => "" }, body: t.ToFullString())).ToArray();
    if (plans.Select(p => p.name).Distinct().Count() != plans.Length) { Console.WriteLine($"SKIP duplicate {file}"); continue; }
    var destinations = plans.Select(p => Path.Combine(Path.GetDirectoryName(file)!, p.name + ".cs")).ToArray();
    if (destinations.Any(p => p != file && File.Exists(p))) { Console.WriteLine($"SKIP existing target {file}"); continue; }
    Console.WriteLine($"SPLIT {file}: {string.Join(", ", plans.Select(p => p.name))}");
    if (dry) continue;
    for (var i = 0; i < plans.Length; i++) File.WriteAllText(destinations[i], prefix + plans[i].body + suffix, new UTF8Encoding(false));
    if (!destinations.Contains(file)) File.Delete(file);
}
