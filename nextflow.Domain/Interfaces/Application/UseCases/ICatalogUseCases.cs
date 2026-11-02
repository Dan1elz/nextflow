using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Categories;
using Nextflow.Domain.Dtos.Products;
using Nextflow.Domain.Dtos.Stock;

namespace Nextflow.Domain.Interfaces.Application.UseCases;

public interface IGetCategoryTreeUseCase
{
    Task<ResultDto<List<CategoryNodeDto>>> Execute(CancellationToken ct = default);
}

/// <summary>Inativa a categoria; com produtos ativos pede confirmação (RN-CE53), reenviada com <paramref name="confirmWarnings"/>.</summary>
public interface IDeleteCategoryUseCase
{
    Task<ResultDto<bool>> Execute(Guid id, bool confirmWarnings, Guid? userId = null, CancellationToken ct = default);
}

/// <summary>Formulário T2: sem <c>id</c> devolve só as opções, para a criação.</summary>
public interface IGetProductFormUseCase
{
    Task<ResultDto<ProductFormDto>> Execute(Guid? id, CancellationToken ct = default);
}

public interface IUpdateProductLifecycleUseCase
{
    Task<ResultDto<bool>> Execute(Guid id, ProductLifecycleRequestDto dto, CancellationToken ct = default);
}

public interface IUploadProductImageUseCase
{
    Task<ResultDto<ProductImageResponseDto>> Execute(Guid id, UploadFileDto? file, CancellationToken ct = default);
}

public interface IRemoveProductImageUseCase
{
    Task<ResultDto<bool>> Execute(Guid id, CancellationToken ct = default);
}

/// <summary>Aba Estoque do T3: matriz variação × local, só da loja informada ou de todas as lojas ativas.</summary>
public interface IGetProductStockUseCase
{
    Task<ResultDto<StockMatrixDto>> Execute(Guid id, Guid? storeId, CancellationToken ct = default);
}
