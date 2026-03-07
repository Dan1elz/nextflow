using Nextflow.Domain.Dtos;
using Nextflow.Domain.Models;
using Nextflow.Infrastructure.Database;

namespace Nextflow.Infrastructure.Seeders;

public class SuppliersSeeder
{
    public static void Seed(AppDbContext context)
    {
        if (context.Suppliers.Any())
        {
            return;
        }

        var suppliers = new Supplier[]
        {
            new(new CreateSupplierDto { Name = "Tech Solutions", CNPJ = "11111111000111" }),
            new(new CreateSupplierDto { Name = "Global Imports", CNPJ = "22222222000122" }),
            new(new CreateSupplierDto { Name = "Nacional Distribuidora", CNPJ = "33333333000133" }),
            new(new CreateSupplierDto { Name = "Fast Componentes", CNPJ = "44444444000144" }),
            new(new CreateSupplierDto { Name = "Mega Varejo", CNPJ = "55555555000155" })
        };

        context.Suppliers.AddRange(suppliers);
        context.SaveChanges();
    }
}
