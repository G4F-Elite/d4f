namespace Engine.Core.Abstractions;

public interface IContentRuntimeFacade
{
    void MountPak(string pakPath);

    void MountDirectory(string directoryPath);

    byte[] ReadFile(string assetPath);
}
