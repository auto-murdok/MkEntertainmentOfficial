using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Architecture conformance (fitness function): the game is ScriptableObject
    /// modular — entities reference injected assets/interfaces, never static
    /// service locators. This test scans every game script type and fails if a
    /// static member exposes its own declaring type (the shape of a singleton
    /// `Instance` service locator), so the modularity rules in AGENTS.md are
    /// enforced by the suite, not by convention alone.
    /// </summary>
    public class ArchitectureConformanceTests
    {
        // Types intentionally allowed to expose a static self-reference.
        // Keep this list EMPTY unless a genuine exception is agreed on.
        private static readonly Type[] AllowedSelfReferencingStatics = new Type[] { };

        [Test]
        public void GameTypes_HaveNoStaticSelfReferencingMembers()
        {
            // Anchor on a known game type: game scripts all live in the same
            // Assembly-CSharp as the core systems.
            Assembly gameAssembly = typeof(Subject<object, object>).Assembly;
            var violations = new List<string>();

            foreach (Type type in gameAssembly.GetTypes())
            {
                if (!type.IsClass || type.IsAbstract) continue;
                if (Array.IndexOf(AllowedSelfReferencingStatics, type) >= 0) continue;

                foreach (FieldInfo field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (field.FieldType == type && field.IsPublic)
                    {
                        violations.Add($"{type.FullName}.{field.Name} (public static field of own type)");
                    }
                }

                foreach (PropertyInfo property in type.GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    MethodInfo getter = property.GetGetMethod(true);
                    if (getter != null && getter.IsStatic && property.PropertyType == type)
                    {
                        violations.Add($"{type.FullName}.{property.Name} (static property returning own type)");
                    }
                }
            }

            Assert.IsEmpty(violations,
                "Static service-locator members are forbidden (SO-architecture rule). Found:\n" +
                string.Join("\n", violations));
        }
    }
}
