using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using LoopEngine.Core;

namespace LoopEngine.Core.EditorTools
{
    /// <summary>
    /// Dibuja un OptionalRef como una sola linea: casilla de activacion + campo de objeto
    /// filtrado por la interfaz del generico.
    ///
    /// CAMBIO C1b (el selector listaba TODO el proyecto):
    /// La version anterior pedia el ObjectField de tipo UnityEngine.Object para poder aceptar
    /// que se soltase un GameObject y resolver el GetComponent a mano. Efecto colateral: el
    /// selector del circulito no filtraba nada y elegir mal solo producia un warning.
    ///
    /// Ahora se hacen las dos cosas por separado:
    ///   1. El ObjectField se pide del tipo de la INTERFAZ, para que el selector filtre.
    ///   2. El arrastre de GameObjects se intercepta antes, a mano, y se resuelve por
    ///      GetComponent. Asi sigue funcionando soltar el objeto desde la jerarquia.
    /// El Coerce se mantiene sobre el valor devuelto como red de seguridad, por si alguna
    /// version de Unity deja pasar por el selector algo que no implementa la interfaz.
    /// </summary>
    [CustomPropertyDrawer(typeof(OptionalRefBase), true)]
    public sealed class OptionalRefDrawer : PropertyDrawer
    {
        private const float ToggleWidth = 18f;
        private const float Gap = 4f;

        private static readonly Dictionary<Type, Type> RequiredTypeCache = new Dictionary<Type, Type>();

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            return IsMisconfigured(property)
                ? line + EditorGUIUtility.standardVerticalSpacing + line
                : line;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty enabledProp = property.FindPropertyRelative("enabled");
            SerializedProperty targetProp = property.FindPropertyRelative("target");

            if (enabledProp == null || targetProp == null)
            {
                EditorGUI.LabelField(position, label.text, "OptionalRef malformado");
                return;
            }

            Type requiredType = ResolveRequiredType(fieldInfo);

            EditorGUI.BeginProperty(position, label, property);

            Rect line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            Rect content = EditorGUI.PrefixLabel(line, new GUIContent(label.text, TooltipFor(requiredType)));

            int previousIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            Rect toggleRect = new Rect(content.x, content.y, ToggleWidth, content.height);
            Rect fieldRect = new Rect(
                content.x + ToggleWidth + Gap,
                content.y,
                Mathf.Max(0f, content.width - ToggleWidth - Gap),
                content.height);

            enabledProp.boolValue = EditorGUI.Toggle(toggleRect, enabledProp.boolValue);

            using (new EditorGUI.DisabledScope(!enabledProp.boolValue))
            {
                if (enabledProp.boolValue)
                {
                    // Antes del ObjectField: si lo que se arrastra es un GameObject o un
                    // componente hermano, se resuelve aqui. El ObjectField tipado por interfaz
                    // no aceptaria el GameObject por si solo.
                    HandleDragAndDrop(fieldRect, requiredType, targetProp);
                }

                UnityEngine.Object current = targetProp.objectReferenceValue;

                bool missing = enabledProp.boolValue && current == null;
                Color previousColor = GUI.color;
                if (missing) GUI.color = new Color(1f, 0.55f, 0.55f);

                // Tipo de la interfaz: el selector solo lista lo que la implementa.
                UnityEngine.Object next = EditorGUI.ObjectField(fieldRect, current, requiredType, true);

                GUI.color = previousColor;

                if (next != current)
                {
                    targetProp.objectReferenceValue = Coerce(next, requiredType, property, warn: true);
                }
            }

            EditorGUI.indentLevel = previousIndent;

            if (IsMisconfigured(property))
            {
                Rect helpRect = new Rect(
                    position.x,
                    line.yMax + EditorGUIUtility.standardVerticalSpacing,
                    position.width,
                    EditorGUIUtility.singleLineHeight);

                EditorGUI.LabelField(helpRect, " ",
                    $"Falta asignar un {requiredType.Name} (o desactiva la casilla).",
                    EditorStyles.miniLabel);
            }

            EditorGUI.EndProperty();
        }

        /// <summary>
        /// Acepta el arrastre de cualquier objeto del que se pueda SACAR la interfaz, aunque
        /// el propio objeto no la implemente (tipicamente un GameObject).
        /// Si no se puede resolver, no se toca el evento: Unity mostrara el cursor de rechazo.
        /// </summary>
        private static void HandleDragAndDrop(Rect rect, Type requiredType, SerializedProperty targetProp)
        {
            Event current = Event.current;

            if (current.type != EventType.DragUpdated && current.type != EventType.DragPerform) return;
            if (!rect.Contains(current.mousePosition)) return;

            UnityEngine.Object[] dragged = DragAndDrop.objectReferences;
            if (dragged == null || dragged.Length == 0) return;

            UnityEngine.Object resolved = Coerce(dragged[0], requiredType, targetProp, warn: false);
            if (resolved == null) return;

            // Si el objeto ya implementa la interfaz, el ObjectField lo aceptaria solo; pero
            // interceptarlo igualmente mantiene el comportamiento uniforme.
            DragAndDrop.visualMode = DragAndDropVisualMode.Link;

            if (current.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                targetProp.objectReferenceValue = resolved;
                current.Use();
            }
        }

        /// <summary>
        /// Normaliza un objeto a algo que implemente 'required', o null.
        /// Con warn:false se usa como consulta silenciosa durante el arrastre; con warn:true,
        /// como red de seguridad tras el selector.
        /// </summary>
        private static UnityEngine.Object Coerce(
            UnityEngine.Object dropped, Type required, SerializedProperty property, bool warn)
        {
            if (dropped == null) return null;
            if (required.IsInstanceOfType(dropped)) return dropped;

            GameObject gameObject = dropped as GameObject;
            if (gameObject == null && dropped is Component component) gameObject = component.gameObject;

            if (gameObject != null)
            {
                // GetComponent acepta un Type de interfaz. Con varios componentes que la
                // implementen en el mismo objeto devuelve el primero; para elegir otro hay que
                // arrastrar el componente concreto desde un segundo inspector.
                Component found = gameObject.GetComponent(required);
                if (found != null) return found;
            }

            if (warn)
            {
                Debug.LogWarning(
                    $"[OptionalRef] '{dropped.name}' no implementa {required.Name}; no se asigno " +
                    $"al campo '{property.propertyPath}'.");
            }

            return null;
        }

        private static bool IsMisconfigured(SerializedProperty property)
        {
            SerializedProperty enabledProp = property.FindPropertyRelative("enabled");
            SerializedProperty targetProp = property.FindPropertyRelative("target");
            if (enabledProp == null || targetProp == null) return false;

            return enabledProp.boolValue && targetProp.objectReferenceValue == null;
        }

        private static string TooltipFor(Type required) =>
            $"Solo acepta objetos que implementen {required.Name}. " +
            "Desmarca la casilla para desactivar esta dependencia.";

        /// <summary>
        /// Saca el argumento generico T de OptionalRef&lt;T&gt; a partir del FieldInfo.
        /// Contempla arrays y List&lt;&gt; para que las colecciones tambien filtren bien, y
        /// tanto el uso directo (OptionalRef&lt;IFoo&gt;) como una subclase concreta.
        /// </summary>
        private static Type ResolveRequiredType(FieldInfo field)
        {
            if (field == null) return typeof(UnityEngine.Object);

            if (RequiredTypeCache.TryGetValue(field.FieldType, out Type cached)) return cached;

            Type type = field.FieldType;

            if (type.IsArray)
            {
                type = type.GetElementType();
            }
            else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                type = type.GetGenericArguments()[0];
            }

            Type result = typeof(UnityEngine.Object);

            while (type != null)
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(OptionalRef<>))
                {
                    result = type.GetGenericArguments()[0];
                    break;
                }

                type = type.BaseType;
            }

            RequiredTypeCache[field.FieldType] = result;
            return result;
        }
    }
}