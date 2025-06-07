#if !UNITY_BURST

namespace Unity.Burst;

[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Struct)]
public sealed class BurstCompileAttribute : Attribute
{

}

#endif
