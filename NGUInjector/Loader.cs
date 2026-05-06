using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NGUInjector
{
    public class Loader
    {
        private static GameObject _load;
        private static Main _reference;
        private static bool _assemblyResolverAttached;

        public static void Init()
        {
            AttachAssemblyResolver();
            _load = new GameObject();
            _reference = _load.AddComponent<Main>();
            Object.DontDestroyOnLoad(_load);
        }

        public static void Unload()
        {
            _reference.Unload();
            _load.SetActive(false);
            Object.Destroy(_load);
        }

        private static void AttachAssemblyResolver()
        {
            if (_assemblyResolverAttached)
                return;

            AppDomain.CurrentDomain.AssemblyResolve += ResolveFromInjectorDirectory;
            _assemblyResolverAttached = true;
        }

        private static Assembly ResolveFromInjectorDirectory(object sender, ResolveEventArgs args)
        {
            try
            {
                var injectorPath = Assembly.GetExecutingAssembly().Location;
                if (string.IsNullOrEmpty(injectorPath))
                    return null;

                var dependencyFile = $"{new AssemblyName(args.Name).Name}.dll";
                var candidate = Path.Combine(Path.GetDirectoryName(injectorPath), dependencyFile);

                return File.Exists(candidate) ? Assembly.LoadFrom(candidate) : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
