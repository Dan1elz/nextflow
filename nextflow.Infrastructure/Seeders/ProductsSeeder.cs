using Nextflow.Domain.Dtos;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Models;
using Nextflow.Infrastructure.Database;

namespace Nextflow.Infrastructure.Seeders;

public class ProductsSeeder
{
    public static void Seed(AppDbContext context)
    {
        if (context.Products.Any())
        {
            return;
        }

        var suppliers = context.Suppliers.ToList();
        if (!suppliers.Any())
        {
            return; 
        }

        var s1 = suppliers.ElementAtOrDefault(0)?.Id ?? suppliers.First().Id;
        var s2 = suppliers.ElementAtOrDefault(1)?.Id ?? suppliers.First().Id;
        var s3 = suppliers.ElementAtOrDefault(2)?.Id ?? suppliers.First().Id;

        var infor = context.Categories.FirstOrDefault(c => c.Description == "Informática");
        var perif = context.Categories.FirstOrDefault(c => c.Description == "Periféricos");
        var elet = context.Categories.FirstOrDefault(c => c.Description == "Eletrônicos");
        var aces = context.Categories.FirstOrDefault(c => c.Description == "Acessórios");
        var hard = context.Categories.FirstOrDefault(c => c.Description == "Hardware");

        var products = new Product[]
        {
            CreateProduct(new CreateProductDto { SupplierId = s1, ProductCode = "PROD-001", Name = "Notebook Dell Inspiron", Description = "Notebook Dell Core i5 8GB RAM 256GB SSD", Quantity = 10, UnitType = UnitType.Unit, Price = 3500.00m }, infor),
            CreateProduct(new CreateProductDto { SupplierId = s1, ProductCode = "PROD-002", Name = "Mouse Sem Fio Logitech", Description = "Mouse sem fio ergonômico 2.4GHz", Quantity = 50, UnitType = UnitType.Unit, Price = 120.00m }, perif, infor),
            CreateProduct(new CreateProductDto { SupplierId = s2, ProductCode = "PROD-003", Name = "Teclado Mecânico Redragon", Description = "Teclado mecânico RGB switch brown", Quantity = 30, UnitType = UnitType.Unit, Price = 250.00m }, perif, infor),
            CreateProduct(new CreateProductDto { SupplierId = s2, ProductCode = "PROD-004", Name = "Monitor LG 24 Polegadas", Description = "Monitor Full HD 75Hz IPS", Quantity = 15, UnitType = UnitType.Unit, Price = 800.00m }, elet, infor),
            CreateProduct(new CreateProductDto { SupplierId = s3, ProductCode = "PROD-005", Name = "Cabo HDMI 2 Metros", Description = "Cabo HDMI 2.0 4K", Quantity = 100, UnitType = UnitType.Unit, Price = 25.00m }, aces),
            CreateProduct(new CreateProductDto { SupplierId = s3, ProductCode = "PROD-006", Name = "Placa de Vídeo RTX 3060", Description = "Nvidia GeForce RTX 3060 12GB", Quantity = 5, UnitType = UnitType.Unit, Price = 2200.00m }, hard, infor),
            CreateProduct(new CreateProductDto { SupplierId = s1, ProductCode = "PROD-007", Name = "Memória RAM 16GB", Description = "Memória DDR4 3200MHz", Quantity = 20, UnitType = UnitType.Unit, Price = 300.00m }, hard, infor)
        };

        context.Products.AddRange(products);
        context.SaveChanges();
    }

    private static Product CreateProduct(CreateProductDto dto, params Category?[] categories)
    {
        var product = new Product(dto);
        foreach (var category in categories)
        {
            if (category != null)
            {
                product.CategoryProducts.Add(new CategoryProduct(new CreateCategoryProductDto 
                { 
                    CategoryId = category.Id, 
                    ProductId = product.Id 
                }));
            }
        }
        return product;
    }
}
