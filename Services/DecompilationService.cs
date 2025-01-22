using System;
using System.IO;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace DotNetReflectCLI.Services
{
    public class DecompilationService
    {
        public CSharpDecompiler CreateDecompiler(string assemblyPath)
        {
            if (!File.Exists(assemblyPath))
            {
                throw new FileNotFoundException($"Assembly file not found: {assemblyPath}");
            }

            try
            {
                var decompilerSettings = new DecompilerSettings
                {
                    ThrowOnAssemblyResolveErrors = false,
                    RemoveDeadCode = true,
                    ShowXmlDocumentation = true
                };

                var module = new PEFile(assemblyPath);
                var resolver = new UniversalAssemblyResolver(assemblyPath, false, module.DetectTargetFrameworkId());
                
                return new CSharpDecompiler(assemblyPath, resolver, decompilerSettings);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to create decompiler for assembly '{assemblyPath}': {ex.Message}", ex);
            }
        }

        public string DecompileType(string assemblyPath, ITypeDefinition type)
        {
            var decompiler = CreateDecompiler(assemblyPath);
            return decompiler.DecompileTypeAsString(new FullTypeName(type.FullName));
        }

        public string DecompileMethod(string assemblyPath, IMethod method)
        {
            var decompiler = CreateDecompiler(assemblyPath);
            return decompiler.DecompileAsString(method.MetadataToken);
        }
    }
} 