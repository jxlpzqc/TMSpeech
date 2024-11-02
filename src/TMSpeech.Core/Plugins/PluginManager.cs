using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using TMSpeech.Core.Services.Resource;

namespace TMSpeech.Core.Plugins
{
    public abstract class PluginManager
    {
        public abstract void LoadPlugins();
        public abstract IReadOnlyList<IPlugin> Plugins { get; }
        public abstract IReadOnlyList<IAudioSource> AudioSources { get; }
        public abstract IReadOnlyList<IRecognizer> Recognizers { get; }
        public abstract IReadOnlyList<ITranslator> Translators { get; }
    }

    class PluginManagerImpl : PluginManager
    {
        internal PluginManagerImpl()
        {
        }

        private record PluginLoadInfo(IPlugin Plugin, Assembly Assembly);

        private List<PluginLoadInfo> _plugins = new List<PluginLoadInfo>();

        public override IReadOnlyList<IPlugin> Plugins => _plugins.Select(u => u.Plugin).ToList();

        public override IReadOnlyList<IAudioSource> AudioSources =>
            _plugins.Select(u => u.Plugin)
                .Where(u => u.GetType().IsAssignableTo(typeof(IAudioSource)))
                .Select(u => (IAudioSource)u).ToList();

        public override IReadOnlyList<IRecognizer> Recognizers =>
            _plugins.Select(u => u.Plugin)
                .Where(u => u.GetType().IsAssignableTo(typeof(IRecognizer)))
                .Select(u => (IRecognizer)u).ToList();

        public override IReadOnlyList<ITranslator> Translators =>
            _plugins.Select(u => u.Plugin)
                .Where(u => u.GetType().IsAssignableTo(typeof(ITranslator)))
                .Select(u => (ITranslator)u).ToList();

        private void RegistErrorHandlers()
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                Debug.WriteLine($"Unhandled exception: {e.ExceptionObject}");
                if (e.ExceptionObject is Exception ex)
                {
                    var execuatblePath = Assembly.GetEntryAssembly().Location;
                    var pluginPath = Path.Combine(Path.GetDirectoryName(execuatblePath), "plugins");
                    if (Path.GetDirectoryName(ex.TargetSite.Module.FullyQualifiedName).Contains(pluginPath))
                    {
                        Debug.WriteLine($"Meet unhandled exception in plugin {ex.TargetSite.Module.Name}");
                        Debug.WriteLine($"Kill process now and disable this plugin next startup");
                    }

                    //TODO: print error message to user
                    Debug.WriteLine("Unhandled exception, exit now");
                    Debug.WriteLine(ex);
                    //Environment.Exit(16);
                }
            };
        }

        private void LoadPlugin(string pluginFile)
        {
            RegistErrorHandlers();
            Assembly assembly;
            var _context = new PluginLoadContext(pluginFile);
            try
            {
                assembly = _context.LoadFromAssemblyPath(pluginFile);
            }
            catch (BadImageFormatException)
            {
                return;
            }

            var types = assembly.GetTypes().Where(t => t.GetInterfaces().Contains(typeof(IPlugin)));
            var assemblyHash = assembly.GetHashCode();
            if (_plugins.Any(u => u.Assembly.GetHashCode() == assemblyHash))
            {
                throw new InvalidOperationException($"Assembly {assembly.FullName} already loaded");
            }

            foreach (var type in types)
            {
                var plugin = (IPlugin?)Activator.CreateInstance(type);
                if (plugin == null)
                {
                    throw new InvalidOperationException($"Can't create instance of {type.FullName}");
                }

                plugin.Init();
                _plugins.Add(new PluginLoadInfo(plugin, assembly));
            }
        }

        class PluginLoadContext : AssemblyLoadContext
        {
            private AssemblyDependencyResolver _resolver;
            private string? _runtimesNativePath = null;

            private string GetRuntimeIdentifier()
            {
                var os = "";
                var arch = "";
                if (OperatingSystem.IsWindows()) os = "win";
                else if (OperatingSystem.IsLinux()) os = "linux";
                else if (OperatingSystem.IsMacOS()) os = "osx";

                if (RuntimeInformation.ProcessArchitecture == Architecture.X64) arch = "x64";
                else if (RuntimeInformation.ProcessArchitecture == Architecture.X86) arch = "x86";
                else if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64) arch = "arm64";
                else if (RuntimeInformation.ProcessArchitecture == Architecture.Arm) arch = "arm";

                if (string.IsNullOrEmpty(os) || string.IsNullOrEmpty(arch)) return "any";
                return string.Format("{0}-{1}", os, arch);
            }

            public PluginLoadContext(string pluginPath)
            {
                _resolver = new AssemblyDependencyResolver(pluginPath);
                var nativeRuntimes = Path.Combine(Path.GetDirectoryName(pluginPath), "runtimes", GetRuntimeIdentifier(), "native");
                if (Directory.Exists(nativeRuntimes)) _runtimesNativePath = nativeRuntimes;
            }

            protected override Assembly Load(AssemblyName assemblyName)
            {
                if (assemblyName.Name == "TMSpeech.Core") return null;
                string assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
                if (assemblyPath != null)
                {
                    return LoadFromAssemblyPath(assemblyPath);
                }

                return null;
            }

            protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
            {
                string? libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
                if (libraryPath != null)
                    return LoadUnmanagedDllFromPath(libraryPath);

                if (_runtimesNativePath != null)
                {
                    try
                    {
                        string libPath = Path.Combine(_runtimesNativePath, $"{unmanagedDllName}.dll");
                        if (File.Exists(libPath)) return LoadUnmanagedDllFromPath(libPath);
                        libPath = Path.Combine(_runtimesNativePath, $"lib{unmanagedDllName}.dll");
                        if (File.Exists(libPath)) return LoadUnmanagedDllFromPath(libPath);
                        libPath = Path.Combine(_runtimesNativePath, $"lib{unmanagedDllName}.so");
                        if (File.Exists(libPath)) return LoadUnmanagedDllFromPath(libPath);
                        libPath = Path.Combine(_runtimesNativePath, $"{unmanagedDllName}.so");
                        if (File.Exists(libPath)) return LoadUnmanagedDllFromPath(libPath);
                        libPath = Path.Combine(_runtimesNativePath, $"{unmanagedDllName}.dylib");
                        if (File.Exists(libPath)) return LoadUnmanagedDllFromPath(libPath);
                        libPath = Path.Combine(_runtimesNativePath, $"lib{unmanagedDllName}.dylib");
                        if (File.Exists(libPath)) return LoadUnmanagedDllFromPath(libPath);
                        return LoadUnmanagedDllFromPath(libraryPath);
                    }
                    catch
                    {
                    }
                }

                return IntPtr.Zero;
            }
        }

        public override void LoadPlugins()
        {
            var execuatblePath = Assembly.GetEntryAssembly().Location;
            var pluginPath = Path.Combine(Path.GetDirectoryName(execuatblePath), "plugins");


            foreach (var dir in Directory.GetDirectories(pluginPath))
            {
                var jsonFileName = Path.Combine(dir, "tmmodule.json");
                if (File.Exists(jsonFileName))
                {
                    string moduleJson = File.ReadAllText(jsonFileName);
                    ModuleInfo moduleInfo;
                    try
                    {
                        moduleInfo = JsonSerializer.Deserialize<ModuleInfo>(moduleJson);
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine($"Error deserialize module info: {e}");
                        continue;
                    }
                    
                    foreach (var assembly in moduleInfo.Assemblies)
                    {
                        try
                        {
                            LoadPlugin(Path.Combine(dir, assembly));
                        }
                        catch (Exception e)
                        {
                            Debug.WriteLine($"Error loading plugin {assembly}: {e}");
                        }
                    }
                }
            }
        }
    }

    public static class PluginManagerFactory
    {
        static Lazy<PluginManager> _pluginManager = new Lazy<PluginManager>(() => new PluginManagerImpl());

        public static PluginManager GetInstance()
        {
            return _pluginManager.Value;
        }
    }
}