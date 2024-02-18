using System;

namespace FoxNuGet.VSSolution
{
    public interface IVSVersionableResource
    {
        string Name { get; }
        SolutionResourceType ResourceType { get; }
        Version Version { get; }
    }
}