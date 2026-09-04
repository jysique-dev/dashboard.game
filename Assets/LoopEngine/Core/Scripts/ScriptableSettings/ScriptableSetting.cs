using System;
using UnityEngine;

namespace LoopEngine.Core
{
    [Serializable]
    public abstract class ScriptableSetting
    {
        [SerializeField] private bool enabled = true;
        public bool Enabled
        {
            get
            {
                return enabled;
            }
            set
            {
                enabled = value;
            }
        }

        // No se serializa (es propiedad, no campo). Cada hija la sobrescribe.
        public virtual Color EditorAccent => new Color(0.35f, 0.60f, 1f);
        public virtual string EditorTitle => GetType().Name;
    }
}
