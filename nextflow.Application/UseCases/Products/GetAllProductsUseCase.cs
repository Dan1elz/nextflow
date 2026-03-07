using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Nextflow.Application.Filters;
using Nextflow.Application.UseCases.Base;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Repositories;
using Nextflow.Domain.Interfaces.Utils;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.Products;

public class GetAllProductsUseCase(IProductRepository repository, IStorageService storageService)
    : GetAllUseCaseBase<Product, IProductRepository, ProductResponseDto>(repository)
{
    protected override ProductResponseDto MapToResponseDto(Product entity)
    {
        var dto = new ProductResponseDto(entity)
        {
            Image = storageService.GetFileUrl(entity.Image)
        };
        return dto;
    }
    protected override Func<IQueryable<Product>, IQueryable<Product>>? GetInclude() => query => query
        .Include(s => s.Supplier)
        .Include(c => c.CategoryProducts)
            .ThenInclude(cp => cp.Category);

    protected override void ApplyFilters(FilterExpressionBuilder<Product> builder, FilterSet filters)
    {
        builder
            .WhereStringContainsAny(filters, "search", p => p.Name, p => p.ProductCode, p => p.Description)
            .WhereStringContains(filters, "productCode", p => p.ProductCode)
            .WhereStringContains(filters, "name", p => p.Name)
            .WhereGuidEquals(filters, "supplierId", p => p.SupplierId)
            .WhereEnumEquals<UnitType>(filters, "unitType", p => p.UnitType)
            .WhereDecimalGte(filters, "priceMin", p => p.Price)
            .WhereDecimalLte(filters, "priceMax", p => p.Price)
            .WhereDecimalGte(filters, "quantityMin", p => p.Quantity)
            .WhereDecimalLte(filters, "quantityMax", p => p.Quantity)
            .WhereNullableDateOnlyLte(filters, "validity", p => p.Validity);

        // categoryId: filtro pela tabela de junção CategoryProducts
        if (filters.TryGetString("categoryId", out var catRaw) && FilterValueParsers.TryParseGuid(catRaw, out var catId))
        {
            Expression<Func<Product, bool>> catFilter = p => p.CategoryProducts.Any(cp => cp.CategoryId == catId);
            builder.And(catFilter);
        }
    }
}
