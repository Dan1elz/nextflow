using Nextflow.Domain.Dtos;
using Nextflow.Domain.Models;
using Nextflow.Infrastructure.Database;

namespace Nextflow.Infrastructure.Seeders;

public class CategoriesSeeder
{
    public static void Seed(AppDbContext context)
    {
        if (context.Categories.Any())
        {
            return;
        }

        var categories = new Category[]
        {
            new(new CreateCategoryDto { Description = "Eletrônicos" }),
            new(new CreateCategoryDto { Description = "Informática" }),
            new(new CreateCategoryDto { Description = "Periféricos" }),
            new(new CreateCategoryDto { Description = "Acessórios" }),
            new(new CreateCategoryDto { Description = "Hardware" })
        };

        context.Categories.AddRange(categories);
        context.SaveChanges();
    }
}
