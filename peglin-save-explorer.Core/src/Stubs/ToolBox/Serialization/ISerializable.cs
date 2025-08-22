// Stub interface for Peglin's ToolBox.Serialization.ISerializable
// This allows our code to compile against the interface that is loaded at runtime from Peglin's assembly

namespace ToolBox.Serialization
{
    /// <summary>
    /// Marker interface used by Peglin's save system to identify serializable objects
    /// The actual implementation is loaded from Peglin's Assembly-CSharp.dll at runtime
    /// </summary>
    public interface ISerializable
    {
    }
}