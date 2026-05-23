using UnityEngine;

namespace UnityMCP.Runtime
{
    /// <summary>
    /// Marks a Unity-serialized field whose persisted inspector value should be replaced by an enforced default.
    /// </summary>
    /// <remarks>
    /// Nexus Unity editor tooling can use this attribute while inspecting or repairing serialized objects so stale saved data
    /// does not override script defaults. Applying it can intentionally discard existing serialized field values, so use it
    /// only when that reset behavior is desired for assets or scene objects.
    /// </remarks>
    public class ForceDefaultAttribute : PropertyAttribute
    {
        /// <summary>
        /// Optional: The value to enforce. If null, the script's initial value is used.
        /// </summary>
        public object DefaultValue { get; private set; }

        /// <summary>
        /// Creates a marker that tells Nexus Unity editor tooling which value should replace stale serialized data.
        /// </summary>
        /// <param name="defaultValue">Optional value to enforce; null means the field's type/default initializer should be used.</param>
        public ForceDefaultAttribute(object defaultValue = null)
        {
            DefaultValue = defaultValue;
        }
    }
}
