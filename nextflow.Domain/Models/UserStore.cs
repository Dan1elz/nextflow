namespace Nextflow.Domain.Models;

/// <summary>Vínculo usuário × loja (decisão 6 de 29/10). Mantido pela API, sem tela: ver <c>IStoreMembership</c>.</summary>
public class UserStore : BaseModel
{
    public Guid UserId { get; private set; }
    public Guid StoreId { get; private set; }

    public virtual Store Store { get; private set; } = null!;

    public override string Preposition => "o";
    public override string Singular => "vínculo de loja";
    public override string Plural => "vínculos de loja";

    private UserStore() { }

    public UserStore(Guid userId, Guid storeId)
    {
        UserId = userId;
        StoreId = storeId;
    }
}
