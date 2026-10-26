using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LSFD.API;
using LSFD.Buildings;
using LSFD.Utils;
using Rage;

namespace LSFD.Core
{
    /// <summary>Keeps the available callouts, including the ones from callout DLLs in Plugins/LSFD/.</summary>
    internal class CalloutRegistry
    {
        private class Entry
        {
            public Type Type;
            public string Name;
            public CalloutProbability Probability;
            public bool RequiresOpenInteriors;
            public CalloutCategory Category;
            public CalloutPriority Priority;

            public bool IsAvailable => (!RequiresOpenInteriors || InteriorSupport.AreInteriorsUnlocked) && Settings.IsCategoryEnabled(Category);
        }

        private readonly List<Entry> entries = new List<Entry>();
        private readonly List<CalloutPack> packs = new List<CalloutPack>();

        public IEnumerable<string> Names => entries.Select(e => e.Name);

        public bool Register(Type type)
        {
            var info = type.GetCustomAttribute<CalloutInfoAttribute>();
            string problem =
                !typeof(Callout).IsAssignableFrom(type) ? "it does not inherit from LSFD.API.Callout" :
                type.IsAbstract ? "it is abstract" :
                type.GetConstructor(Type.EmptyTypes) == null ? "it has no public parameterless constructor" :
                info == null ? "it has no [CalloutInfo] attribute" :
                entries.Any(e => e.Name.Equals(info.Name, StringComparison.OrdinalIgnoreCase)) ? $"a callout named '{info.Name}' already exists" :
                null;

            if (problem != null)
            {
                Game.LogTrivial($"LSFD: could not register callout {type.FullName}: {problem}.");
                return false;
            }

            entries.Add(new Entry { Type = type, Name = info.Name, Probability = info.Probability, RequiresOpenInteriors = info.RequiresOpenInteriors,
                Category = info.Category, Priority = info.Priority });
            Game.LogTrivial($"LSFD: registered callout '{info.Name}' ({type.Assembly.GetName().Name}).");
            return true;
        }

        public void RegisterFromAssembly(Assembly assembly)
        {
            foreach (Type type in GetLoadableTypes(assembly).Where(t => !t.IsAbstract))
            {
                if (type.GetCustomAttribute<CalloutInfoAttribute>() != null)
                    Register(type);
                else if (typeof(CalloutPack).IsAssignableFrom(type))
                    StartPack(type);
            }
        }

        /// <summary>Loads every DLL in Plugins/LSFD/ and registers the callouts in it.</summary>
        public void LoadCalloutAssemblies()
        {
            if (!Directory.Exists(XmlStore.DataFolder))
                return;

            // Callout DLLs reference LSFD.dll; make sure they bind to this already loaded copy.
            AppDomain.CurrentDomain.AssemblyResolve += ResolveLsfd;

            foreach (string path in Directory.GetFiles(XmlStore.DataFolder, "*.dll"))
            {
                try
                {
                    RegisterFromAssembly(Assembly.LoadFrom(path));
                }
                catch (Exception e)
                {
                    Game.LogTrivial($"LSFD: could not load callouts from {Path.GetFileName(path)}: {e.Message}");
                }
            }
        }

        public Callout CreateRandom()
        {
            var available = entries.Where(e => e.IsAvailable).ToList();
            int total = available.Sum(e => (int)e.Probability);
            if (total == 0)
                return null;

            int roll = MathHelper.GetRandomInteger(total);
            foreach (Entry entry in available)
            {
                roll -= (int)entry.Probability;
                if (roll < 0)
                    return Create(entry);
            }
            return null;
        }

        public Callout Create(string name)
        {
            Entry entry = entries.FirstOrDefault(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (entry != null && entry.RequiresOpenInteriors && !InteriorSupport.AreInteriorsUnlocked)
            {
                Game.LogTrivial($"LSFD: '{entry.Name}' needs the Enable All Interiors mod, which was not found.");
                return null;
            }
            return entry == null ? null : Create(entry);
        }

        public void Shutdown()
        {
            foreach (CalloutPack pack in packs)
            {
                try
                {
                    pack.Finally();
                }
                catch (Exception e)
                {
                    Game.LogTrivial($"LSFD: {pack.GetType().FullName}.Finally() threw: {e}");
                }
            }
            packs.Clear();
            AppDomain.CurrentDomain.AssemblyResolve -= ResolveLsfd;
        }

        private static Callout Create(Entry entry)
        {
            var callout = (Callout)Activator.CreateInstance(entry.Type);
            callout.Initialize(entry.Name, entry.Category, entry.Priority);
            return callout;
        }

        private void StartPack(Type type)
        {
            try
            {
                var pack = (CalloutPack)Activator.CreateInstance(type);
                pack.Initialize();
                packs.Add(pack);
                Game.LogTrivial($"LSFD: started callout pack {type.FullName}.");
            }
            catch (Exception e)
            {
                Game.LogTrivial($"LSFD: could not start callout pack {type.FullName}: {e}");
            }
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.Where(t => t != null);
            }
        }

        private static Assembly ResolveLsfd(object sender, ResolveEventArgs args)
        {
            Assembly lsfd = typeof(CalloutRegistry).Assembly;
            return new AssemblyName(args.Name).Name == lsfd.GetName().Name ? lsfd : null;
        }
    }
}
