namespace Nextflow.Domain.Interfaces.Infrastructure.Services;

/// <summary>Hash de senha (BCrypt). A senha nunca é guardada nem logada em texto puro (RN-AC05).</summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}
