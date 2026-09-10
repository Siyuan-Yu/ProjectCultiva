using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

/// <summary>
/// Minimal headless runner for the project's EditMode NUnit tests (Mono / net472).
///
/// Unity Test Runner needs the interactive Editor to release the project lock (Unity batchmode
/// also cannot reach its licensing IPC channel in a confined sandbox), so this runner reflects
/// over XianXia.Tests.dll and executes the selected [Test] methods directly.  Compile the test
/// assembly with tools/offline-compile.ps1 -DropUnityEditor so the tests use the
/// XIANXIA_BASEGAME / HeadlessContentRoot injection instead of UnityEngine.Application.dataPath.
///
/// Usage: see tools/run-headless-tests.ps1
/// </summary>
class Program
{
    sealed class TestCase
    {
        public Type Type;
        public MethodInfo Method;
        public string Name;
    }

    static int Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += Resolve;
        Directory.SetCurrentDirectory(ProjectRoot());

        var asm = Assembly.LoadFrom(Path.Combine(
            ProjectRoot(), "Library", "ScriptAssemblies", "XianXia.Tests.dll"));

        var filters = args;
        var tests = new List<TestCase>();
        InjectHeadlessContentRoot(asm);
        foreach (var type in asm.GetTypes().OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            if (type.IsAbstract || !type.IsClass)
                continue;
            if (filters.Length > 0 && !Matches(type, filters))
                continue;
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!HasAttribute(method, "TestAttribute"))
                    continue;
                tests.Add(new TestCase { Type = type, Method = method, Name = type.Name + "." + method.Name });
            }
        }

        Console.WriteLine("discovered " + tests.Count + " tests in " +
                          tests.Select(t => t.Type.Name).Distinct().Count() + " classes");

        var passed = 0;
        var failed = new List<string>();
        foreach (var test in tests)
        {
            object instance = null;
            try
            {
                instance = Activator.CreateInstance(test.Type);
                foreach (var setup in SetUpMethods(test.Type))
                    setup.Invoke(instance, null);
                test.Method.Invoke(instance, null);
                passed++;
                Console.WriteLine("PASS " + test.Name);
            }
            catch (Exception ex)
            {
                var inner = ex is TargetInvocationException && ex.InnerException != null
                    ? ex.InnerException
                    : ex;
                failed.Add(test.Name + " :: " + inner.GetType().Name + ": " + inner.Message);
                Console.WriteLine("FAIL " + test.Name + " :: " + inner.GetType().Name + ": " + inner.Message);
                if (!string.IsNullOrEmpty(inner.StackTrace))
                {
                    var frames = inner.StackTrace.Split('\n');
                    var limit = Math.Min(6, frames.Length);
                    for (var i = 0; i < limit; i++)
                        Console.WriteLine("     " + frames[i].Trim());
                }
            }
            finally
            {
                if (instance != null)
                {
                    foreach (var tearDown in TearDownMethods(test.Type))
                    {
                        try { tearDown.Invoke(instance, null); } catch { }
                    }
                    var disposable = instance as IDisposable;
                    if (disposable != null)
                    {
                        try { disposable.Dispose(); } catch { }
                    }
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine("TOTAL=" + tests.Count + " PASSED=" + passed + " FAILED=" + failed.Count);
        foreach (var failure in failed)
            Console.WriteLine("  " + failure);
        return failed.Count == 0 ? 0 : 1;
    }

    /// <summary>
    /// 部分测试类用 <c>public static string HeadlessContentRoot</c> 注入内容根，避免在 Unity 之外
    /// JIT 到 UnityEngine.Application.dataPath（ECall）。Unity Test Runner 下保持 null，行为不变。
    /// </summary>
    static void InjectHeadlessContentRoot(Assembly asm)
    {
        var root = Environment.GetEnvironmentVariable("XIANXIA_BASEGAME");
        if (string.IsNullOrEmpty(root))
            root = Path.Combine(ProjectRoot(), "Content", "BaseGame");
        foreach (var type in asm.GetTypes())
        {
            var field = type.GetField("HeadlessContentRoot",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
            if (field != null && field.FieldType == typeof(string))
                field.SetValue(null, root);
        }
    }

    static bool Matches(Type type, string[] filters)
    {
        var full = type.FullName ?? string.Empty;
        foreach (var filter in filters)
            if (full.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        return false;
    }

    static bool HasAttribute(MemberInfo member, string attributeName)
    {
        foreach (var attribute in member.GetCustomAttributes(true))
            if (attribute.GetType().Name == attributeName)
                return true;
        return false;
    }

    static IEnumerable<MethodInfo> SetUpMethods(Type type) =>
        type.GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(m => HasAttribute(m, "SetUpAttribute"));

    static IEnumerable<MethodInfo> TearDownMethods(Type type) =>
        type.GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(m => HasAttribute(m, "TearDownAttribute"));

    static string ProjectRoot()
    {
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        for (var i = 0; i < 8 && dir != null; i++)
        {
            if (Directory.Exists(Path.Combine(dir, "Library", "ScriptAssemblies")) &&
                Directory.Exists(Path.Combine(dir, "Content", "BaseGame")))
                return dir;
            dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
        }

        return @"D:\UnityProjects\XianXia";
    }

    static Assembly Resolve(object sender, ResolveEventArgs args)
    {
        var simpleName = new AssemblyName(args.Name).Name;
        var candidates = new[]
        {
            Path.Combine(ProjectRoot(), "Library", "ScriptAssemblies", simpleName + ".dll"),
            Path.Combine(ProjectRoot(), "Library", "PackageCache", "com.unity.ext.nunit@1.0.6", "net35",
                "unity-custom", simpleName + ".dll"),
            @"D:\UnityEditor\2022.3.6f1\Editor\Data\Managed\UnityEngine\" + simpleName + ".dll",
            @"D:\UnityEditor\2022.3.6f1\Editor\Data\Managed\" + simpleName + ".dll"
        };
        foreach (var candidate in candidates)
            if (File.Exists(candidate))
                return Assembly.LoadFrom(candidate);
        return null;
    }
}
