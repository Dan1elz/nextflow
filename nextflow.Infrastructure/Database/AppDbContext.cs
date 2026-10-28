using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nextflow.Domain.Models;

namespace Nextflow.Infrastructure.Database;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Address> Addresses { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<CategoryProduct> CategoryProducts { get; set; }
    public DbSet<City> Cities { get; set; }
    public DbSet<Client> Clients { get; set; }
    public DbSet<Contact> Contacts { get; set; }
    public DbSet<Country> Countries { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
    public DbSet<PurchaseItem> PurchaseItems { get; set; }
    public DbSet<Sale> Sales { get; set; }
    public DbSet<State> States { get; set; }
    public DbSet<StockMovement> StockMovements { get; set; }
    public DbSet<Supplier> Suppliers { get; set; }
    public DbSet<User> Users { get; set; }

    // *** Módulo de Previsão de Demanda (IA) ***
    public DbSet<Promotion> Promotions { get; set; }
    public DbSet<ProductSalesMonthly> ProductSalesMonthly { get; set; }
    public DbSet<ProductSeasonality> ProductSeasonalities { get; set; }
    public DbSet<ForecastRun> ForecastRuns { get; set; }
    public DbSet<ForecastRecommendation> ForecastRecommendations { get; set; }
    public DbSet<ForecastProjection> ForecastProjections { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();
        modelBuilder.Entity<User>().HasIndex(u => u.CPF).IsUnique();

        modelBuilder.Entity<Client>().HasIndex(u => u.CPF).IsUnique();

        modelBuilder.Entity<Supplier>().HasIndex(u => u.CNPJ).IsUnique();

        modelBuilder.Entity<Product>().HasIndex(p => p.ProductCode).IsUnique();

        // Consultas por período (dashboard, histórico de vendas da previsão, lead time de compras)
        modelBuilder.Entity<Order>().HasIndex(o => new { o.Status, o.CreateAt });
        modelBuilder.Entity<Sale>().HasIndex(s => new { s.IsActive, s.CreateAt });
        modelBuilder.Entity<PurchaseOrder>().HasIndex(p => new { p.PurchaseStatus, p.ReceivedAt });

        // *** Módulo de Previsão de Demanda (IA) ***
        modelBuilder.Entity<Promotion>().HasIndex(p => new { p.ProductId, p.StartDate, p.EndDate });

        modelBuilder.Entity<ProductSalesMonthly>().HasIndex(s => new { s.ProductId, s.Year, s.Month }).IsUnique();

        modelBuilder.Entity<ProductSeasonality>().HasIndex(s => new { s.ProductId, s.Month }).IsUnique();

        modelBuilder.Entity<ForecastRecommendation>().HasIndex(r => r.Status);
        modelBuilder.Entity<ForecastRecommendation>()
            .HasOne(r => r.PurchaseOrder)
            .WithMany()
            .HasForeignKey(r => r.PurchaseOrderId)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<ForecastRecommendation>()
            .HasOne(r => r.Promotion)
            .WithMany()
            .HasForeignKey(r => r.PromotionId)
            .OnDelete(DeleteBehavior.SetNull);

        base.OnModelCreating(modelBuilder);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.LogTo(Console.WriteLine, LogLevel.Warning); //tirar depois
        optionsBuilder.EnableSensitiveDataLogging(); // tirar depois
        base.OnConfiguring(optionsBuilder);
    }
}
