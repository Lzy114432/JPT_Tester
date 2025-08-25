using Ewan.Core;
using Ewan.Core.Attribute;
using System;
using System.Linq;
using System.Reflection;

namespace Ewan.BusinessBonding
{
    public class MainController : BaseManager<MainController>
    {
        public override bool Init()
        {
            return base.Init();
        }

        public override void Destroy()
        {
            var types =
                from assembly in AppDomain.CurrentDomain.GetAssemblies()
                from type in assembly.GetTypes()
                from attributes in type.GetCustomAttributes(false)
                where ((attributes.GetType() == typeof(ManagerAttribute)) && ((ManagerAttribute)attributes).IsEnable)
                select type;
            foreach (var type in types)
            {
                DestroyObj(type);
            }
            base.Destroy();
        }
        public bool Initialize()
        {
            var types =
                  from assembly in AppDomain.CurrentDomain.GetAssemblies()
                  from type in assembly.GetTypes()
                  from attributes in type.GetCustomAttributes(false)
                  where ((attributes.GetType() == typeof(ManagerAttribute)) && ((ManagerAttribute)attributes).IsEnable)
                  orderby ((ManagerAttribute)attributes).Priority
                  select type;

            int index = 0;
            foreach (var type in types)
            {
                if (!InitObject(type))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 初始化一个对象.
        /// </summary>
        /// <param name="t">对象类型</param>
        private bool InitObject(Type t)
        {
            Type baseType = IsGenericSubclassOf(t, typeof(BaseManager<>));
            if (baseType != null)
            {
                if (t.IsAbstract)
                {
                    return true;
                }

                MethodInfo instance = baseType.GetMethod("Instance");
                Object obj = instance.Invoke(null, null);

                MethodInfo method = baseType.GetMethod("Init");
                return (bool)method.Invoke(obj, null);
            }
            return false;
        }

        /// <summary>
        /// 销毁对象.
        /// </summary>
        /// <param name="t"></param>
        private void DestroyObj(Type t)
        {
            Type baseType = IsGenericSubclassOf(t, typeof(BaseManager<>));
            if (baseType != null)
            {
                if (t.IsAbstract)
                {
                    return;
                }

                MethodInfo instance = baseType.GetMethod("Instance");
                Object obj = instance.Invoke(null, null);

                MethodInfo method = baseType.GetMethod("Destroy");
                method.Invoke(obj, null);
            }
        }

        private Type IsGenericSubclassOf(Type type, Type superType)
        {
            if (type.BaseType != null
                && type.BaseType != typeof(object)
                && type.BaseType.IsGenericType)
            {
                if (type.BaseType.GetGenericTypeDefinition() == superType)
                {
                    return type.BaseType;
                }

                return IsGenericSubclassOf(type.BaseType, superType);
            }

            return null;
        }
    }
}
