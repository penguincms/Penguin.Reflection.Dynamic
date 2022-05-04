using Microsoft.CSharp;
using System;
using System.CodeDom.Compiler;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace Penguin.Reflection.Dynamic
{
    [SuppressMessage("Microsoft.Naming", "CA1724:TypeNamesShouldNotMatchNamespaces")]
    public static class CodeGenerator
    {
        private static readonly ConcurrentDictionary<string, MethodInfo> Precompiled = new ConcurrentDictionary<string, MethodInfo>();

        //Double brace to keep string format from bitching
        public static Dictionary<TemplateType, string> Templates { get; } = new Dictionary<TemplateType, string>()
        {
            [TemplateType.Standard] = "using System; namespace _{2} {{ public static class Ex {{ public static {0} Main() {{ {1} }} }} }}"
        };

        public enum TemplateType
        {
            Standard
        }

        public static T Execute<T>(string body, TemplateType templateType = TemplateType.Standard)
        {
            if (body is null)
            {
                throw new ArgumentNullException(nameof(body));
            }

            object o = Execute(body, typeof(T), templateType);

            if (o is null)
            {
                return default;
            }

            return (T)o;
        }

        public static object Execute(string body, Type t, TemplateType templateType = TemplateType.Standard)
        {
            if (t is null)
            {
                throw new ArgumentNullException(nameof(t));
            }

            string ns = Guid.NewGuid().ToString().Replace("-", "");
            string Code = string.Format(CultureInfo.CurrentCulture, Templates[templateType], t.FullName, body, ns);

            if (!Precompiled.TryGetValue(Code, out MethodInfo m))
            {
                return CompileAndRun(ns, Code);
            }
            else
            {
                return m.Invoke(null, Array.Empty<object>());
            }
        }

        /// <summary>
        /// This accepts an array of strings but currently only assumes one string (file) for the purposes of the cache.
        /// If more files are added this will likely break it
        /// </summary>
        /// <param name="Namespace"></param>
        /// <param name="code"></param>
        /// <returns></returns>
        private static object CompileAndRun(string Namespace, params string[] code)
        {
            CompilerParameters CompilerParams = new CompilerParameters
            {
                GenerateInMemory = true,
                TreatWarningsAsErrors = false,
                GenerateExecutable = false,
                CompilerOptions = "/optimize"
            };

            string[] references = { "System.dll" };
            CompilerParams.ReferencedAssemblies.AddRange(references);

            using (CSharpCodeProvider provider = new CSharpCodeProvider())
            {
                CompilerResults compile = provider.CompileAssemblyFromSource(CompilerParams, code);

                if (compile.Errors.HasErrors)
                {
                    string text = "Compile error: ";
                    foreach (CompilerError ce in compile.Errors)
                    {
                        text += "rn" + ce?.ToString();
                    }
                    throw new Exception(text);
                }

                MethodInfo methInfo = compile.CompiledAssembly.GetModules()[0]?
                                     .GetType("_" + Namespace + ".Ex")?
                                     .GetMethod("Main");

                if (methInfo != null)
                {
                    _ = Precompiled.TryAdd(code.First(), methInfo);

                    return methInfo.Invoke(null, Array.Empty<object>());
                }
            }

            return default;
        }
    }
}