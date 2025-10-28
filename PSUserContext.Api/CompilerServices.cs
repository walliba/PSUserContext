// ReSharper disable CheckNamespace
// Language extensions
namespace System.Runtime.CompilerServices;

internal static class IsExternalInit {}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
internal sealed class RequiredMemberAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
internal sealed class CompilerFeatureRequiredAttribute : Attribute
{
    public string FeatureName { get; }
    public bool   IsOptional  { get; init; }
    public CompilerFeatureRequiredAttribute(string featureName) => FeatureName = featureName;
}