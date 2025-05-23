#nullable enable

using System.Collections.Generic;
using System.Linq;
using JetBrains.Application.Parts;
using JetBrains.Application.Threading;
using JetBrains.Collections;
using JetBrains.Lifetimes;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Plugins.Unity.Core.ProjectModel;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Caches;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Files;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.Util.Collections;
using JetBrains.Util.PersistentMap;

namespace JetBrains.ReSharper.Plugins.Unity.CSharp.Caches
{
    [SolutionComponent(Instantiation.DemandAnyThreadSafe)]
    public class UnityShortcutCache : SimpleICache<CountingSet<string>>
    {
        private readonly CountingSet<string> myLocalCache = new();
        private readonly OneToCompactCountingSet<string, IPsiSourceFile> myFilesWithShortCut = new();

        public UnityShortcutCache(Lifetime lifetime, IShellLocks shellLocks, IPersistentIndexManager persistentIndexManager)
            : base(lifetime, shellLocks, persistentIndexManager,  CreateMarshaller())
        {
        }


        private static IUnsafeMarshaller<CountingSet<string>> CreateMarshaller()
        {
            return new UniversalMarshaller<CountingSet<string>>(reader =>
                {
                    var count = reader.ReadInt32();
                    var set = new CountingSet<string>(count);
                    for (int i = 0; i < count; i++)
                    {
                        var key = reader.ReadString();
                        if (key != null)
                            set.Add(key, reader.ReadInt32());
                    }

                    return set;
                },
                (writer, value) =>
                {
                    writer.Write(value.Count);
                    foreach (var (item, count) in value)
                    {
                        writer.Write(item);
                        writer.Write(count);
                    }
                });
        }


        protected override bool IsApplicable(IPsiSourceFile sf)
        {
            return base.IsApplicable(sf) && sf.PrimaryPsiLanguage.Is<CSharpLanguage>() && sf.GetProject().IsUnityProject();
        }

        public override object? Build(IPsiSourceFile sourceFile, bool isStartup)
        {
            var file = sourceFile.GetDominantPsiFile<CSharpLanguage>();
            if (file == null)
                return null;

            var childrenEnumerator = file.Descendants();
            var result = new CountingSet<string>();

            while (childrenEnumerator.MoveNext())
            {
                var current = childrenEnumerator.Current;
                switch (current)
                {
                    case IChameleonNode:
                        childrenEnumerator.SkipThisNode();
                        break;
                    case IMethodDeclaration methodDeclaration:
                        // we could not resolve here: this means if user uses name alias we will not find this attribute
                        // SwaExtension??
                        var attribute = methodDeclaration.Attributes.FirstOrDefault(t => t.Name?.NameIdentifier?.Name.Equals("MenuItem") == true);
                        if (attribute != null)
                        {
                            var arguments = attribute.Arguments;
                            var validateArgument = GetArgument(1, "isValidateFunction", arguments);
                            var isValidateFunction = validateArgument?.Value?.ConstantValue.IsTrue();
                            if (!isValidateFunction.HasValue || !isValidateFunction.Value)
                            {
                                var constantValue = GetArgument(0, "itemName", arguments)?.Value?.ConstantValue;
                                if (constantValue != null && constantValue.IsString(out var nameValue) && nameValue != null)
                                {
                                    var shortcut = ExtractShortcutFromName(nameValue);
                                    if (shortcut != null)
                                    {
                                        result.Add(shortcut);
                                    }
                                }
                            }
                        }
                        childrenEnumerator.SkipThisNode();
                        break;
                }
            }

            if (result.Count > 0)
                return result;

            return null;
        }


        public static string? ExtractShortcutFromName(string name)
        {
            var parts = name.Split(' ');
            if (parts.Length == 1)
                return null;

            var shortCut = parts[^1];
            if (shortCut.Length == 0)
                return null;

            if (shortCut[0] == '_' || shortCut[0] == '&' || shortCut[0] == '#' || shortCut[0] == '%')
            {
                return shortCut;
            }

            return null;
        }


        public static ICSharpArgument? GetArgument(int position, string name, IList<ICSharpArgument> arguments)
        {
            var fromName = arguments.FirstOrDefault(t => name.Equals(t?.NameIdentifier?.Name));
            if (fromName != null)
                return fromName;
            var positionalArguments = arguments.Where(x => !x.IsNamedArgument).ToList();
            return positionalArguments.Count <= position ? null : positionalArguments[position];
        }

        public override void Merge(IPsiSourceFile sourceFile, object? builtPart)
        {
            RemoveFromLocalCache(sourceFile);
            base.Merge(sourceFile, builtPart);
            if (builtPart is CountingSet<string> set)
            {
                AddToLocalCache(sourceFile, set);
            }
        }

        public override void MergeLoaded(object data)
        {
            base.MergeLoaded(data);
            PopulateLocalCache();
        }

        public override void Drop(IPsiSourceFile sourceFile)
        {
            RemoveFromLocalCache(sourceFile);
            base.Drop(sourceFile);
        }

        private void PopulateLocalCache()
        {
            foreach (var (file, cacheItems) in Map)
                AddToLocalCache(file, cacheItems);
        }

        private void AddToLocalCache(IPsiSourceFile sourceFile, CountingSet<string> set)
        {
            foreach (var (value, c) in set)
            {
                myLocalCache.Add(value, c);
                myFilesWithShortCut.Add(value, sourceFile, c);
            }
        }

        private void RemoveFromLocalCache(IPsiSourceFile sourceFile)
        {
            if (Map.TryGetValue(sourceFile, out var set))
            {
                foreach (var (value, c) in set)
                {
                    myLocalCache.Add(value, -c);
                    myFilesWithShortCut.Add(value, sourceFile, -c);
                }
            }
        }


        public int GetCount(string shortcut) => myLocalCache.GetCount(shortcut);

        public IEnumerable<IPsiSourceFile> GetSourceFileWithShortCut(string shortCut) => myFilesWithShortCut.GetValues(shortCut);
    }
}
