using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OcrSharp.Domain.Interfaces.Repositories;
using OcrSharp.Domain.Interfaces.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace OcrSharp.Infra.CrossCutting.IoC.Extensions
{
    internal static class ServiceCollectionExtensions
    {
        private static readonly string Prefix = typeof(ServiceCollectionExtensions).Namespace?.Split('.')[0]; // Uppermost namespace
        private static readonly string Sufix = string.Empty;
        private static readonly IEnumerable<Assembly> AllAssemblies;

        static ServiceCollectionExtensions()
        {
            AllAssemblies = GetSolutionAssemblies().Where(a => a.FullName.StartsWith(Prefix.Trim(), StringComparison.InvariantCultureIgnoreCase));
        }

        private static IEnumerable<Assembly> GetSolutionAssemblies()
        {
            var assemblies = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "*.dll")
                .Where(a => Path.GetFileName(a.ToLower()).StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
                .Select(x => Assembly.Load(AssemblyName.GetAssemblyName(x)));
            return assemblies.ToArray();
        }

        public static void UseRepositoriesAndServices(this IServiceCollection services, ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            var classesImplementingInterfaces = AllAssemblies.SelectMany(t =>
                    t.ExportedTypes.Select(y => y.GetTypeInfo()).Where(x =>
                        x.IsPublic
                        && !x.IsInterface
                        && !x.IsAbstract
                        && x.GetInterfaces().Any(i => i == typeof(IDomainRepository) || i == typeof(IDomainService))
                        ))
                .ToList();

            classesImplementingInterfaces.ForEach(assignedTypes =>
            {
                var allInterfaces = assignedTypes.ImplementedInterfaces
                    .Where(x => x != typeof(IDomainRepository) && x != typeof(IDomainService))
                    .Select(i => i.GetTypeInfo());

                foreach (var serviceType in allInterfaces)
                {
                    if (!assignedTypes.IsGenericType)
                    {
                        services.TryAdd(new ServiceDescriptor(serviceType, assignedTypes, lifetime));
                    }
                    else
                    {
                        var arguments = serviceType.GetGenericTypeDefinition().GetGenericArguments();
                        var combinedType = serviceType.GetGenericTypeDefinition().MakeGenericType(arguments);
                        services.TryAdd(new ServiceDescriptor(combinedType, assignedTypes, lifetime));
                    }
                }
            });
        }
    }
}
