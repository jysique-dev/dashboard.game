using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace LoopEngine.Core
{
    public static class SceneValidation
    {
        private const BindingFlags Flags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        /// <summary>Anade a 'issues' los problemas del componente. No limpia la lista.</summary>
        public static void Collect(MonoBehaviour behaviour, List<ValidationIssue> issues)
        {
            if (behaviour == null || issues == null) return;

            string componentName = behaviour.GetType().Name;
            Type type = behaviour.GetType();

            while (type != null && type != typeof(MonoBehaviour) && type != typeof(Behaviour)
                   && type != typeof(Component) && type != typeof(UnityEngine.Object))
            {
                FieldInfo[] fields = type.GetFields(Flags);

                for (int i = 0; i < fields.Length; i++)
                {
                    FieldInfo field = fields[i];
                    if (!IsSerializedByUnity(field)) continue;

                    object value;
                    try { value = field.GetValue(behaviour); }
                    catch { continue; }

                    Inspect(behaviour, componentName, field, value, issues);
                }

                type = type.BaseType;
            }
        }

        private static void Inspect(
            MonoBehaviour behaviour,
            string componentName,
            FieldInfo field,
            object value,
            List<ValidationIssue> issues)
        {
            // Caso 1: OptionalRef suelto.
            if (value is OptionalRefBase optional)
            {
                CheckOptional(behaviour, componentName, field.Name, optional, issues);
                return;
            }

            // Caso 2: coleccion de OptionalRef (array o List). Solo primer nivel.
            if (value is IEnumerable enumerable && value is not string)
            {
                int index = 0;
                foreach (object element in enumerable)
                {
                    if (element is OptionalRefBase item)
                    {
                        CheckOptional(behaviour, componentName, $"{field.Name}[{index}]", item, issues);
                    }
                    index++;
                }
                return;
            }

            if (field.GetCustomAttribute<RequiredRefAttribute>() == null) return;

            bool missing = value == null || (value is UnityEngine.Object reference && reference == null);

            if (missing)
            {
                issues.Add(new ValidationIssue(
                    behaviour, componentName, field.Name,
                    "campo obligatorio sin asignar."));
            }
        }

        private static void CheckOptional(
            MonoBehaviour behaviour,
            string componentName,
            string fieldName,
            OptionalRefBase optional,
            List<ValidationIssue> issues)
        {
            if (!optional.IsMisconfigured) return;

            issues.Add(new ValidationIssue(
                behaviour, componentName, fieldName,
                $"activado pero sin asignar (se esperaba {optional.RequiredType.Name}). " +
                "Asigna el objeto o desactiva la casilla."));
        }

        /// <summary>
        /// Replica la regla de Unity: publico y no marcado [NonSerialized], o privado con
        /// [SerializeField]. No es exhaustiva (no filtra tipos no serializables) pero para
        /// nuestros dos casos basta.
        /// </summary>
        private static bool IsSerializedByUnity(FieldInfo field)
        {
            if (field.IsStatic) return false;
            if (field.GetCustomAttribute<NonSerializedAttribute>() != null) return false;
            if (field.IsPublic) return true;
            return field.GetCustomAttribute<SerializeField>() != null;
        }
    }
}