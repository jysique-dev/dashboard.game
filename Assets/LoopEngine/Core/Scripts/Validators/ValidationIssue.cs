namespace LoopEngine.Core
{
    /// <summary>Un problema de cableado encontrado en un componente.</summary>
    public readonly struct ValidationIssue
    {
        public readonly UnityEngine.Object Context;
        public readonly string ComponentName;
        public readonly string FieldName;
        public readonly string Message;

        public ValidationIssue(UnityEngine.Object context, string componentName, string fieldName, string message)
        {
            Context = context;
            ComponentName = componentName;
            FieldName = fieldName;
            Message = message;
        }

        public override string ToString() => $"{ComponentName}.{FieldName}: {Message}";
    }
}