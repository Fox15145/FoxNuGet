namespace FoxNuGet.VSSolution
{
    public interface IVSResource
    {
        string Name { get; }
        SolutionResourceType ResourceType { get; }
    }
}