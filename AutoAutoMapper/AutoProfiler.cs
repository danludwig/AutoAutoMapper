using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AutoMapper;

namespace AutoAutoMapper
{
    /// <summary>
    /// Helper class for scanning assemblies and automatically adding AutoMapper.Profile
    /// implementations to the AutoMapper Configuration.
    /// </summary>
    public static class AutoProfiler
    {
        /// <summary>
        /// Scans all types in each Assembly argument for AutoMapper Profile classes
        /// and adds each to the AutoMapper Configuration.
        /// </summary>
        /// <param name="assemblies">
        /// The assemblies to scan for AutoMapper Profile classes. To only scan the
        /// calling assembly, this argument can be omitted.
        /// </param>
        public static void RegisterProfiles(params Assembly[] assemblies)
        {
            RegisterProfiles(EnsureAssembly(assemblies, Assembly.GetCallingAssembly()));
        }

        /// <summary>
        /// Scans all types in the assemblies argument for AutoMapper Profile classes
        /// and adds each to the AutoMapper Configuration.
        /// </summary>
        /// <param name="assemblies">
        /// The assemblies to scan for AutoMapper Profile classes. When this argument
        /// is null or empty, the calling assembly will be scanned by default.
        /// </param>
        public static void RegisterProfiles(IEnumerable<Assembly> assemblies)
        {
            Mapper.Initialize(configuration =>
                GetConfiguration(Mapper.Configuration, EnsureAssembly(assemblies, Assembly.GetCallingAssembly())));
        }

        /// <summary>
        /// In order for Assembly.GetCallingAssembly() to correctly reference this class' consumer, it must
        /// be invoked from a public method. Otherwise, Assembly.GetCallingAssembly() will reference this
        /// assembly, which obviously contains no AutoMapper.Profile implementations.
        ///
        /// This is a helper method for substituting the consuming assembly when no other assemblies are
        /// explicitly passed to any of the public RegisterProfiles method overloads.
        /// </summary>
        /// <param name="argAssemblies">
        /// The assemblies that the consuming assembly explcitly asked us to scan.
        /// </param>
        /// <param name="callingAssembly">
        /// The consuming assembly.
        /// </param>
        /// <returns>
        /// An enumeration of assemblies to be scanned for AutoMapper.Profile class implementations.
        /// </returns>
        private static IEnumerable<Assembly> EnsureAssembly(IEnumerable<Assembly> argAssemblies, Assembly callingAssembly)
        {
            // In order for GetCallingAssembly to work, it must be invoked from a public method.
            // This is why the callingAssembly arg is passed from public methods.

            // create an initial default empty array of assemblies in case argAssemblies is null
            var assembliesArray = new Assembly[] { };

            // when argAssemblies is not null, enumerate it as an array to prevent multiple enumerations
            if (argAssemblies != null)
                assembliesArray = argAssemblies.ToArray();

            // when there are no assemblies explicitly defined, return the calling assembly only
            if (!assembliesArray.Any())
                return new[] { callingAssembly };

            // otherwise, return the explicitly defined assemblies
            return assembliesArray;
        }

        /// <summary>
        /// Enumerates over all relevant assemblies, scanning each for non-abstract implementations of
        /// AutoMapper.Profile. For each match that is found, a new instance is constructed and added
        /// to the AutoMapper configuration.
        /// </summary>
        /// <param name="configuration">
        /// The AutoMapper configuration.
        /// </param>
        /// <param name="assemblies">
        /// Assemblies to scan for concrete AutoMapper.Profile implementations.
        /// </param>
        private static void GetConfiguration(IConfiguration configuration, IEnumerable<Assembly> assemblies)
        {
            foreach (var assembly in assemblies)
                foreach (var profileClass in GetProfileClassesFrom(assembly))
                    configuration.AddProfile((Profile)Activator.CreateInstance(profileClass));
        }

        private static IEnumerable<Type> GetProfileClassesFrom(Assembly assembly)
        {
            return assembly.GetTypes()
                .Where(t =>
                    t != typeof (Profile)
                    && typeof (Profile).IsAssignableFrom(t)
                    && !t.IsAbstract
                );
        }
    }
}
