using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ClassLibrary1;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;

namespace CompileLinkAndRun
{
    class Fun
    {
        private bool IsThisAppNetCore()
        {
            var trustedAssembliesPaths = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
            return trustedAssembliesPaths != null;
        }

        public void ReferencedFrameworkAssemblies(List<MetadataReference> all_references)
        {
            List<string> result = new List<string>();
            if (IsThisAppNetCore())
            {
                var trustedAssembliesPaths = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
                var s = trustedAssembliesPaths as string;
                var l = s.Split(Path.PathSeparator);
                result = l.ToList();
                result = result.Where(x => !x.Contains("System.Private.CoreLib")).ToList();
            }
            else
            {
                string mscorlib = "";
                if (IntPtr.Size == 4)
                {
                    // 32-bit
                    mscorlib = @"C:\Windows\Microsoft.NET\assembly\GAC_32\mscorlib\v4.0_4.0.0.0__b77a5c561934e089\mscorlib.dll";
                }
                else if (IntPtr.Size == 8)
                {
                    // 64-bit
                    mscorlib = @"C:\Windows\Microsoft.NET\assembly\GAC_64\mscorlib\v4.0_4.0.0.0__b77a5c561934e089\mscorlib.dll";
                }
                else
                    throw new Exception();
                result.Add(mscorlib);
            }

            foreach (var r in result)
            {
                var jj = Assembly.LoadFrom(r);
                all_references.Add(MetadataReference.CreateFromFile(jj.Location));
            }
        }

        private void FixUpMetadataReferences(List<MetadataReference> all_references, Type type)
        {
            var stack = new Stack<Assembly>();
            Assembly a = type.Assembly;
            HashSet<Assembly> visited = new HashSet<Assembly>();
            stack.Push(a);
            while (stack.Any())
            {
                var t = stack.Pop();
                if (visited.Contains(t)) continue;
                visited.Add(t);
                if (t.Location.Contains("netstandard.dll"))
                {
                    ReferencedFrameworkAssemblies(all_references);
                }

                // Very important note:
                // typeof(object).GetTypeInfo().Assembly This series of evaluations is
                // grabbing the implementation assemblies for the various types. Hence your
                // code is passing a set of implementation assemblies to the compiler,
                // not reference assemblies.This isn't a supported scenario for the DLLs,
                // particularly for System.Object.

                all_references.Add(MetadataReference.CreateFromFile(t.Location));
                foreach (var r in a.GetReferencedAssemblies())
                {
                    AssemblyName q = r;
                    var jj = Assembly.Load(q);
                    stack.Push(jj);
                }
            }
        }

        public void CompileLinkRun()
        {
            var code = @"
using System;

namespace ClassLibrary2
{
    public class Class2
    {
        public void Doit()
        {
            System.Console.WriteLine(""hi there"");
        }
    }
}";
            string code_path = @"c:\temp\" + Path.GetRandomFileName();
            code_path = Path.ChangeExtension(code_path, "cs");
            var workspace = new AdhocWorkspace();
            string projectName = "Project";
            ProjectId projectId = ProjectId.CreateNewId();
            VersionStamp versionStamp = VersionStamp.Create();
            ProjectInfo helloWorldProject = ProjectInfo.Create(projectId, versionStamp, projectName,
                projectName, LanguageNames.CSharp);
            SourceText sourceText = SourceText.From(code, Encoding.UTF8);
            Project newProject = workspace.AddProject(helloWorldProject);
            Document newDocument = workspace.AddDocument(newProject.Id, code_path, sourceText);
            SyntaxNode syntaxRoot = newDocument.GetSyntaxRootAsync().Result;

            string assemblyName = Path.GetRandomFileName();
            assemblyName = Path.ChangeExtension(assemblyName, "dll");
            string symbolsName = Path.ChangeExtension(assemblyName, "pdb");

            Type type = typeof(Class1);
            var dependencies = new List<MetadataReference>();
            FixUpMetadataReferences(dependencies, type);
            FixUpMetadataReferences(dependencies, typeof(System.Object));

            CSharpCompilation compilation = CSharpCompilation.Create(
                assemblyName,
                syntaxTrees: new[] { syntaxRoot.SyntaxTree },
                references: dependencies.ToArray(),
                options: new CSharpCompilationOptions(
                        OutputKind.DynamicallyLinkedLibrary)
                    .WithOptimizationLevel(OptimizationLevel.Debug)
            );
            Assembly assembly = null;
            using (var assemblyStream = new MemoryStream())
            using (var symbolsStream = new MemoryStream())
            {
                var emit_options = new EmitOptions(
                    debugInformationFormat: DebugInformationFormat.PortablePdb
                    , pdbFilePath: symbolsName
                );

                EmitResult result = compilation.Emit(
                    peStream: assemblyStream,
                    pdbStream: symbolsStream,
                    options: emit_options);

                if (!result.Success)
                {
                    IEnumerable<Diagnostic> failures = result.Diagnostics.Where(diagnostic =>
                        diagnostic.IsWarningAsError ||
                        diagnostic.Severity == DiagnosticSeverity.Error);

                    foreach (Diagnostic diagnostic in failures)
                    {
                        Console.Error.WriteLine("{0}: {1}", diagnostic.Id, diagnostic.GetMessage());
                    }
                }
                else
                {
                    assemblyStream.Seek(0, SeekOrigin.Begin);
                    symbolsStream.Seek(0, SeekOrigin.Begin);
                    assembly = Assembly.Load(assemblyStream.ToArray(), symbolsStream.ToArray());
                }
            }

            var types = assembly.GetTypes();
            foreach (var t2 in types)
            {
                System.Console.WriteLine(t2.FullName);
            }

            string name = "ClassLibrary2.Class2";
            Type t = assembly.GetType(name);
            string method_name = "Doit";
            MethodInfo method_info = t.GetMethod(method_name);
            if (method_info == null) throw new Exception();
            Type it = t;
            object instance = Activator.CreateInstance(it);
            object[] a = new object[] { };
            var res = method_info.Invoke(instance, a);
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var fun = new Fun();
            fun.CompileLinkRun();
        }
    }
}
