using System;
using UnityEngine;

namespace LoopEngine.CraftingEngine
{
    /// <summary>
    /// Common base for every authored asset in the crafting system
    /// (items, recipes, machines, categories, modifiers).
    /// Owns the string id and its cached <see cref="CraftingId"/>.
    /// </summary>
    public abstract class CraftingDefinition : ScriptableObject, ISerializationCallbackReceiver
    {
        [SerializeField, Tooltip("Stable, unique identifier. Never change it once content ships.")]
        private string _id = string.Empty;

        [SerializeField, Tooltip("Human readable name. Falls back to the id when empty.")]
        private string _displayName = string.Empty;

        [NonSerialized] private CraftingId _cachedId;

        /// <summary>Hashed identifier used by every registry and runtime lookup.</summary>
        public CraftingId Id
        {
            get
            {
                // Covers assets created in memory via CreateInstance, where
                // OnAfterDeserialize is never invoked.
                if (!_cachedId.IsValid)
                    _cachedId = new CraftingId(_id);

                return _cachedId;
            }
        }

        /// <summary>The raw authoring string, exactly as stored on the asset.</summary>
        public string RawId => _id ?? string.Empty;

        /// <summary>Display name, or the raw id when no name was authored.</summary>
        public string DisplayName => string.IsNullOrEmpty(_displayName) ? RawId : _displayName;

        /// <summary>
        /// The display name field exactly as stored, empty included.
        /// <see cref="DisplayName"/> substitutes the id when this is empty, which is right for
        /// UI and wrong for export: writing the id back would turn "no name" into a real name.
        /// </summary>
        public string RawDisplayName => _displayName ?? string.Empty;

        /// <summary>True when the asset carries a usable id.</summary>
        public bool IsValid => Id.IsValid && ValidateContent();

        /// <summary>
        /// Override to add per-type validity rules (non-empty outputs, positive duration...).
        /// Called by <see cref="IsValid"/> and by the editor validator.
        /// </summary>
        protected virtual bool ValidateContent() => true;

        // Runs on the loading thread. Only pure string work happens here, no Unity API.
        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            _cachedId = new CraftingId(_id);
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        protected virtual void OnValidate()
        {
            if (!string.IsNullOrEmpty(_id))
            {
                string trimmed = _id.Trim();
                if (!string.Equals(trimmed, _id, StringComparison.Ordinal))
                    _id = trimmed;
            }

            _cachedId = new CraftingId(_id);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only mutation used by the edit-mode toolkit and the JSON importer.
        /// Not available in builds so runtime code cannot rename content.
        /// </summary>
        internal void EditorSetIdentity(string id, string displayName)
        {
            _id = string.IsNullOrEmpty(id) ? string.Empty : id.Trim();
            _displayName = displayName ?? string.Empty;
            _cachedId = new CraftingId(_id);
        }
#endif
    }
}