using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LSFD.API;
using LSFD.Buildings;
using LSFD.Callouts.Xml;
using LSFD.UI;
using LSFD.Utils;
using Rage;

namespace LSFD.Core
{
    /// <summary>
    /// Keeps the available callouts: the built-in ones, the ones from callout DLLs in Plugins/LSFD/Callouts/ and
    /// the data-driven ones from the XML files in that folder.
    /// </summary>
    internal class CalloutRegistry
    {
        private class Entry
        {
            public Func<Callout> Factory;
            public string Source;
            public bool IsXml;
            public string Name;
            public CalloutProbability Probability;
            public bool RequiresOpenInteriors;
            public bool AllowAmbient;
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

            Add(new Entry { Factory = () => (Callout)Activator.CreateInstance(type), Source = $"{type.FullName} ({type.Assembly.GetName().Name})",
                Name = info.Name, Probability = info.Probability, RequiresOpenInteriors = info.RequiresOpenInteriors,
                AllowAmbient = info.AllowAmbient, Category = info.Category, Priority = info.Priority });
            return true;
        }

        /// <summary>Registers a callout defined in an XML file.</summary>
        public bool Register(XmlCalloutDefinition definition, string file)
        {
            string problem =
                string.IsNullOrWhiteSpace(definition.Name) ? "it has no Name" :
                entries.Any(e => e.Name.Equals(definition.Name, StringComparison.OrdinalIgnoreCase)) ? $"a callout named '{definition.Name}' already exists" :
                definition.Validate();

            if (problem != null)
            {
                Game.LogTrivial($"LSFD: could not register callout '{definition.Name}' from {file}: {problem}.");
                return false;
            }

            Add(new Entry { Factory = () => new XmlCallout(definition), Source = file, IsXml = true, Name = definition.Name, Probability = definition.Probability,
                RequiresOpenInteriors = false, AllowAmbient = definition.Ambient, Category = definition.Category, Priority = definition.Priority });
            return true;
        }

        public void RegisterFromAssembly(Assembly assembly)
        {
            List<Type> types = GetLoadableTypes(assembly).Where(t => !t.IsAbstract).ToList();

            // Packs first: when the DLL needs a different LSFD, none of it is loaded, rather than half of it crashing later.
            var assemblyPacks = new List<CalloutPack>();
            foreach (Type type in types.Where(t => typeof(CalloutPack).IsAssignableFrom(t)))
            {
                CalloutPack pack = CreatePack(type);
                if (pack == null)
                    continue;
                if (!IsSupported(pack, assembly))
                    return;
                assemblyPacks.Add(pack);
            }

            foreach (Type type in types.Where(t => t.GetCustomAttribute<CalloutInfoAttribute>() != null))
                Register(type);

            foreach (CalloutPack pack in assemblyPacks)
                StartPack(pack);
        }

        /// <summary>Loads the callouts from the XML files in Plugins/LSFD/Callouts/; returns how many were registered.</summary>
        public int LoadXmlCallouts()
        {
            int registered = 0;
            if (!Directory.Exists(ModFolder.Callouts))
                return registered;

            foreach (string path in Directory.GetFiles(ModFolder.Callouts, "*.xml"))
            {
                string file = Path.GetFileName(path);
                XmlCalloutFile callouts = XmlCalloutFile.Load(path);
                if (callouts == null)
                {
                    Notify.Warning($"Could not read the callouts in ~y~{file}~s~, see RagePluginHook.log.");
                    continue;
                }

                registered += callouts.Callouts.Count(definition => Register(definition, file));
            }
            return registered;
        }

        /// <summary>Forgets the XML callouts and loads the files again, e.g. after editing them.</summary>
        public int ReloadXmlCallouts()
        {
            entries.RemoveAll(e => e.IsXml);
            return LoadXmlCallouts();
        }

        /// <summary>Loads every DLL in Plugins/LSFD/Callouts/ and registers the callouts in it.</summary>
        public void LoadCalloutAssemblies()
        {
            if (!Directory.Exists(ModFolder.Callouts))
                return;

            // Callout DLLs reference LSFD.dll; make sure they bind to this already loaded copy.
            AppDomain.CurrentDomain.AssemblyResolve += ResolveLsfd;

            foreach (string path in Directory.GetFiles(ModFolder.Callouts, "*.dll"))
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

        public Callout CreateRandom() => CreateRandom(e => true);

        /// <summary>A random callout that can also be an incident the player comes across.</summary>
        public Callout CreateRandomAmbient() => CreateRandom(e => e.AllowAmbient);

        private Callout CreateRandom(Func<Entry, bool> filter)
        {
            var available = entries.Where(e => e.IsAvailable && e.Probability != CalloutProbability.Never && filter(e)).ToList();
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

        private void Add(Entry entry)
        {
            entries.Add(entry);
            Game.LogTrivial($"LSFD: registered callout '{entry.Name}' ({entry.Source}).");
        }

        private Callout Create(Entry entry)
        {
            try
            {
                Callout callout = entry.Factory();
                callout.Initialize(entry.Name, entry.Category, entry.Priority);
                return callout;
            }
            catch (Exception e)
            {
                // Usually a DLL built against an LSFD API that has changed (MissingMethodException, TypeLoadException):
                // take it out of the rotation instead of failing every time it is picked.
                entries.Remove(entry);
                Game.LogTrivial($"LSFD: callout '{entry.Name}' ({entry.Source}) could not be created and was disabled: {e}");
                return null;
            }
        }

        private static CalloutPack CreatePack(Type type)
        {
            try
            {
                return (CalloutPack)Activator.CreateInstance(type);
            }
            catch (Exception e)
            {
                Game.LogTrivial($"LSFD: could not create callout pack {type.FullName}: {e}");
                return null;
            }
        }

        private static bool IsSupported(CalloutPack pack, Assembly assembly)
        {
            int version;
            try
            {
                version = pack.ApiVersion;
            }
            catch (Exception e)
            {
                Game.LogTrivial($"LSFD: could not read the API version of {pack.GetType().FullName}: {e.Message}");
                return false;
            }

            string name = assembly.GetName().Name;
            string problem =
                version > Functions.InstalledApiVersion ? $"needs a newer LSFD (API version {version}, this LSFD has {Functions.InstalledApiVersion}). Update LSFD." :
                version < Functions.OldestSupportedApiVersion ? $"was made for API version {version}, which this LSFD no longer supports. Ask its author for an update." :
                null;

            if (problem == null)
                return true;

            Game.LogTrivial($"LSFD: callout pack {name} was not loaded: it {problem}");
            Notify.Warning($"Callout pack ~y~{name}~s~ was not loaded: it {problem}");
            return false;
        }

        private void StartPack(CalloutPack pack)
        {
            try
            {
                pack.Initialize();
                packs.Add(pack);
                Game.LogTrivial($"LSFD: started callout pack {pack.GetType().FullName} (API version {pack.ApiVersion}).");
            }
            catch (Exception e)
            {
                Game.LogTrivial($"LSFD: could not start callout pack {pack.GetType().FullName}: {e}");
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
