using EwanCore.Attribute;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace EwanCore.Bootstrap
{
    /// <summary>
    /// Manager 类型扫描器：扫描带 <see cref="ManagerAttribute"/> 的类型并按 Priority 排序。
    /// </summary>
    public static class ManagerTypeScanner
    {
        /// <summary>
        /// 扫描当前 AppDomain（或指定程序集）中标记了 <see cref="ManagerAttribute"/> 的 Manager 类型，并按 Priority 排序。
        /// </summary>
        public static IReadOnlyList<Type> Discover(IEnumerable<Assembly> assemblies = null)
        {
            assemblies ??= AppDomain.CurrentDomain.GetAssemblies();

            var result = new List<(Type Type, int Priority)>();
            foreach (var assembly in assemblies)
            {
                foreach (var type in SafeGetTypes(assembly))
                {
                    var attr = type.GetCustomAttribute<ManagerAttribute>(inherit: false);
                    if (attr == null || !attr.IsEnable)
                    {
                        continue;
                    }

                    result.Add((type, attr.Priority));
                }
            }

            return result
                .OrderBy(x => x.Priority)
                .ThenBy(x => x.Type.FullName, StringComparer.Ordinal)
                .Select(x => x.Type)
                .ToList();
        }

        private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
        {
            if (assembly == null)
            {
                return Array.Empty<Type>();
            }

            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null);
            }
            catch
            {
                return Array.Empty<Type>();
            }
        }
    }
}
