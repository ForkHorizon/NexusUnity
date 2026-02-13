using UnityEngine;

namespace UnityMCP.Runtime
{
    /// <summary>
    /// Attribute to mark fields that should be reset to their default values 
    /// regardless of what is saved in the inspector serialization.
    /// This helps prevent "Serialization Ghosts" where C# changes don't apply to existing objects.
    /// </summary>
    public class ForceDefaultAttribute : PropertyAttribute
    {
        /// <summary>
        /// Optional: The value to enforce. If null, the script's initial value is used.
        /// </summary>
        public object DefaultValue { get; private set; }

        /// <summary>
        /// Marks a field to be enforced to its default value.
        /// </summary>
        public ForceDefaultAttribute(object defaultValue = null)
        {
            DefaultValue = defaultValue;
        }
    }
}
