using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Models;
using Nextflow.Domain.Models.Base;
using Nextflow.Domain.Utils;
using Nextflow.Infrastructure.Database;

namespace Nextflow.Infrastructure.Seeders.Demo;

/// <summary>
/// Dados de demonstração: loja fictícia de doces e presentes com 36 meses de operação, para a previsão de demanda (IA).
/// <list type="bullet">
/// <item>Só roda com <c>Seed:Demo:Enabled = true</c> e em banco sem a categoria "Doces e Presentes" (idempotente).</item>
/// <item>Reproduzível: <c>new Random(42)</c> + <c>Seed:Demo:AnchorDate</c> (mesma âncora ⇒ mesmos dados).</item>
/// <item>Espelha os fluxos reais: venda gera movimentação <c>Sales</c> por item; compra recebida gera <c>Entry</c>,
/// grava <c>ReceivedAt</c> e atualiza o custo. O estoque de cada produto é a soma das movimentações (RNF002),
/// começando por um <c>Adjustment</c> de saldo de abertura.</item>
/// <item>Cenários para a banca: Comprar, Promover (excesso e validade), Manter, produto novo, em alta, em queda,
/// ruptura histórica e promoções passadas (<see cref="DemoScenario"/>).</item>
/// <item>Tudo em uma transação, gravado em lotes: em caso de erro nada é gravado.</item>
/// </list>
/// </summary>
public class DemoStoreSeeder(AppDbContext context, IOptions<DemoSeedSettings> options, ILogger<DemoStoreSeeder> logger) : ISeeder
{
    private const int OrdersPerBatch = 500;
    private const int PurchasesPerBatch = 100;

    private readonly AppDbContext _context = context;
    private readonly DemoSeedSettings _settings = options.Value;
    private readonly ILogger<DemoStoreSeeder> _logger = logger;
    // Semente fixa: mesma âncora ⇒ mesmos dados. Instância nova por execução (seeder é Scoped)
    private readonly Random _rng = new(42);
    private Guid _adminId;

    public int Order => 70;

    public async Task SeedAsync(CancellationToken ct)
    {
        if (!_settings.Enabled)
            return;

        if (await _context.Categories.AnyAsync(c => c.Description == DemoStoreCatalog.RootCategory, ct))
            return;

        var admin = await _context.Users.Where(u => u.Role == RoleEnum.Admin).OrderBy(u => u.CreateAt).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("O seed de demonstração precisa de um usuário Admin (UsersSeeder).");
        _adminId = admin.Id;

        var stopwatch = Stopwatch.StartNew();
        var simulation = new DemoStoreSimulation(_settings.AnchorDate ?? BusinessClock.Today, _rng);
        simulation.Run();

        var autoDetectChanges = _context.ChangeTracker.AutoDetectChangesEnabled;
        _context.ChangeTracker.AutoDetectChangesEnabled = false;

        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            var summary = await PersistAsync(simulation, ct);
            await transaction.CommitAsync(ct);
            _logger.LogInformation("Dados de demonstração criados em {ElapsedMs} ms: {Summary}.", stopwatch.ElapsedMilliseconds, summary);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
        finally
        {
            _context.ChangeTracker.Clear();
            _context.ChangeTracker.AutoDetectChangesEnabled = autoDetectChanges;
        }
    }

    private async Task<string> PersistAsync(DemoStoreSimulation simulation, CancellationToken ct)
    {
        var registrationDate = simulation.FirstDay.AddDays(-60);

        var suppliers = await CreateSuppliersAsync(registrationDate, ct);
        var categories = await CreateCategoriesAsync(registrationDate, ct);
        var products = CreateProducts(simulation, suppliers, categories);
        var clients = await CreateClientsAsync(simulation.FirstDay, ct);
        await SaveAndClearAsync(ct);

        CreatePromotions(simulation, products);
        CreateOpeningBalances(simulation, products);
        CreateSupplierReturns(simulation, products);
        await SaveAndClearAsync(ct);

        var (received, pending) = await CreatePurchaseOrdersAsync(simulation, products, suppliers, ct);
        var (orders, items) = await CreateSalesOrdersAsync(simulation, products, clients, ct);

        return $"{products.Count} produtos, {clients.Count} clientes, {orders} pedidos de venda ({items} itens), " +
               $"{received} compras recebidas e {pending} em trânsito, {simulation.Promotions.Count} promoções históricas, " +
               $"{simulation.LostUnits} un. de venda perdida por ruptura, período {simulation.FirstDay:dd/MM/yyyy}–{simulation.LastDay:dd/MM/yyyy}";
    }

    // ------------------------------------------------------------------ cadastros

    private async Task<List<Supplier>> CreateSuppliersAsync(DateOnly registrationDate, CancellationToken ct)
    {
        var usedCnpjs = (await _context.Suppliers.Select(s => s.CNPJ).ToListAsync(ct)).ToHashSet();
        var result = new List<Supplier>();

        foreach (var demo in DemoStoreCatalog.Suppliers)
        {
            string cnpj;
            do { cnpj = GenerateCnpj(); } while (!usedCnpjs.Add(cnpj));

            var supplier = new Supplier(new CreateSupplierDto { Name = demo.Name, CNPJ = cnpj, LeadTimeDays = demo.LeadTimeDays });
            _context.Suppliers.Add(supplier);
            Backdate(supplier, ToUtc(registrationDate, 9, 0));
            result.Add(supplier);
        }

        return result;
    }

    private async Task<Dictionary<string, Category>> CreateCategoriesAsync(DateOnly registrationDate, CancellationToken ct)
    {
        var existing = (await _context.Categories
                .Where(c => DemoStoreCatalog.Categories.Contains(c.Description))
                .ToListAsync(ct))
            .GroupBy(c => c.Description)
            .ToDictionary(g => g.Key, g => g.First());

        var result = new Dictionary<string, Category>();
        foreach (var description in DemoStoreCatalog.Categories)
        {
            if (existing.TryGetValue(description, out var category))
            {
                result[description] = category;
                continue;
            }

            category = new Category(new CreateCategoryDto { Description = description });
            _context.Categories.Add(category);
            Backdate(category, ToUtc(registrationDate, 9, 30));
            result[description] = category;
        }

        return result;
    }

    private List<Product> CreateProducts(DemoStoreSimulation simulation, List<Supplier> suppliers, Dictionary<string, Category> categories)
    {
        var result = new List<Product>();

        for (var p = 0; p < DemoStoreCatalog.Products.Length; p++)
        {
            var demo = DemoStoreCatalog.Products[p];
            var product = new Product(new CreateProductDto
            {
                SupplierId = suppliers[demo.SupplierIndex].Id,
                ProductCode = demo.Code,
                Name = demo.Name,
                Description = demo.Description,
                // Saldo final da simulação = soma das movimentações gravadas abaixo
                Quantity = simulation.FinalStock[p],
                UnitType = UnitType.Unit,
                Price = demo.Price,
                Validity = simulation.Validity(p),
                Brand = demo.Brand,
                // Último custo recebido, como o recebimento real de pedido de compra faz
                CostPrice = simulation.LastCost[p],
                MinStock = demo.MinStock
            });

            foreach (var description in new[] { DemoStoreCatalog.RootCategory, demo.Category })
            {
                product.CategoryProducts.Add(new CategoryProduct(new CreateCategoryProductDto
                {
                    CategoryId = categories[description].Id,
                    ProductId = product.Id
                }));
            }

            _context.Products.Add(product);
            Backdate(product, ToUtc(simulation.LaunchDate[p].AddDays(-7), 10, 0));
            result.Add(product);
        }

        return result;
    }

    private async Task<List<Client>> CreateClientsAsync(DateOnly firstDay, CancellationToken ct)
    {
        var usedCpfs = (await _context.Clients.Select(c => c.CPF).ToListAsync(ct))
            .Select(OnlyDigits)
            .ToHashSet();
        var result = new List<Client>();

        foreach (var (name, lastName) in DemoStoreCatalog.Clients)
        {
            string cpf;
            do { cpf = GenerateCpf(); } while (!usedCpfs.Add(cpf));

            var birthDate = new DateOnly(1965 + _rng.Next(0, 36), _rng.Next(1, 13), _rng.Next(1, 29));
            var client = new Client(new CreateClientDto { Name = name, LastName = lastName, CPF = cpf, BirthDate = birthDate });
            _context.Clients.Add(client);
            Backdate(client, ToUtc(firstDay.AddDays(-_rng.Next(5, 40)), 11, 0));
            result.Add(client);
        }

        return result;
    }

    // ------------------------------------------------------------------ promoções e estoque inicial

    private void CreatePromotions(DemoStoreSimulation simulation, List<Product> products)
    {
        foreach (var period in simulation.Promotions)
        {
            var promotion = new Promotion(new CreatePromotionDto
            {
                ProductId = products[period.Product].Id,
                DiscountPercent = period.DiscountPercent,
                StartDate = period.Start,
                EndDate = period.End,
                Reason = period.Reason,
                Source = PromotionSource.Manual,
                CreatedByUserId = _adminId
            });
            _context.Promotions.Add(promotion);
            Backdate(promotion, ToUtc(period.Start.AddDays(-3), 15, 0));
        }
    }

    private void CreateOpeningBalances(DemoStoreSimulation simulation, List<Product> products)
    {
        foreach (var opening in simulation.Openings)
        {
            var movement = new StockMovement(new CreateStockMovementDto
            {
                ProductId = products[opening.Product].Id,
                Quantity = opening.Quantity,
                MovementType = MovementType.Adjustment,
                Description = "Saldo de abertura (dados de demonstração)",
                UserId = _adminId,
                Quote = opening.Cost
            });
            _context.StockMovements.Add(movement);
            Backdate(movement, ToUtc(simulation.DateOf(opening.Day), 7, 30));
        }
    }

    private void CreateSupplierReturns(DemoStoreSimulation simulation, List<Product> products)
    {
        foreach (var supplierReturn in simulation.SupplierReturns)
        {
            var movement = new StockMovement(new CreateStockMovementDto
            {
                ProductId = products[supplierReturn.Product].Id,
                Quantity = supplierReturn.Quantity,
                MovementType = MovementType.Exit,
                Description = "Devolução ao fornecedor: sobra da temporada (dados de demonstração)",
                UserId = _adminId,
                Quote = supplierReturn.Cost
            });
            _context.StockMovements.Add(movement);
            Backdate(movement, ToUtc(simulation.DateOf(supplierReturn.Day), 17, 0));
        }
    }

    // ------------------------------------------------------------------ pedidos de compra

    private async Task<(int Received, int Pending)> CreatePurchaseOrdersAsync(
        DemoStoreSimulation simulation, List<Product> products, List<Supplier> suppliers, CancellationToken ct)
    {
        int received = 0, pending = 0, batch = 0;

        foreach (var purchase in simulation.Purchases.OrderBy(x => x.OrderDay))
        {
            var purchaseOrder = new PurchaseOrder(suppliers[purchase.Supplier].Id, purchase.Note);
            decimal total = 0;
            foreach (var line in purchase.Lines)
            {
                purchaseOrder.PurchaseItems.Add(new PurchaseItem(purchaseOrder.Id, products[line.Product].Id, line.Quantity, 0, line.Cost));
                total += line.Quantity * line.Cost;
            }
            purchaseOrder.SetTotalAmount(total);

            var createdAt = ToUtc(simulation.DateOf(purchase.OrderDay), 9, _rng.Next(0, 60));
            _context.PurchaseOrders.Add(purchaseOrder);
            Backdate(purchaseOrder, createdAt);
            foreach (var item in purchaseOrder.PurchaseItems)
                Backdate(item, createdAt);

            if (purchase.Received)
            {
                var receivedAt = ToUtc(simulation.DateOf(purchase.ReceiveDay), _rng.Next(8, 12), _rng.Next(0, 60));
                purchaseOrder.MarkAsReceived();
                _context.Entry(purchaseOrder).Property(x => x.ReceivedAt).CurrentValue = receivedAt;
                _context.Entry(purchaseOrder).Property(x => x.UpdateAt).CurrentValue = receivedAt;

                foreach (var line in purchase.Lines)
                {
                    var movement = new StockMovement(new CreateStockMovementDto
                    {
                        ProductId = products[line.Product].Id,
                        Quantity = line.Quantity,
                        MovementType = MovementType.Entry,
                        Description = $"Entrada de estoque via Pedido de Compra {purchaseOrder.Id}",
                        UserId = _adminId,
                        Quote = line.Cost
                    });
                    _context.StockMovements.Add(movement);
                    Backdate(movement, receivedAt);
                }
                received++;
            }
            else
            {
                purchaseOrder.Update(new UpdatePurchaseOrderDto { Status = PurchaseStatus.Pending });
                _context.Entry(purchaseOrder).Property(x => x.UpdateAt).CurrentValue = createdAt;
                pending++;
            }

            if (++batch % PurchasesPerBatch == 0)
                await SaveAndClearAsync(ct);
        }

        await SaveAndClearAsync(ct);
        return (received, pending);
    }

    // ------------------------------------------------------------------ pedidos de venda

    private async Task<(int Orders, int Items)> CreateSalesOrdersAsync(
        DemoStoreSimulation simulation, List<Product> products, List<Client> clients, CancellationToken ct)
    {
        var catalog = DemoStoreCatalog.Products;
        // Poucos clientes compram muito, muitos compram pouco
        var clientWeights = Enumerable.Range(0, clients.Count).Select(i => (int)Math.Round(40 * Math.Pow(0.9, i)) + 1).ToArray();
        var totalClientWeight = clientWeights.Sum();
        int ordersCount = 0, itemsCount = 0, pendingOrders = 0;

        for (var d = 0; d < simulation.Days; d++)
        {
            var date = simulation.DateOf(d);

            // Quebra as vendas do dia em linhas (produto, quantidade)
            var lines = new List<(int Product, int Quantity)>();
            for (var p = 0; p < catalog.Length; p++)
            {
                var remaining = simulation.Sold[p, d];
                while (remaining > 0)
                {
                    var quantity = Math.Min(remaining, _rng.Next(1, catalog[p].MaxUnitsPerLine + 1));
                    lines.Add((p, quantity));
                    remaining -= quantity;
                }
            }
            Shuffle(lines);

            // Agrupa as linhas em pedidos de 1 a 4 itens (produtos distintos)
            while (lines.Count > 0)
            {
                var size = PickOrderSize();
                var picked = new List<(int Product, int Quantity)>(size);
                for (var i = 0; i < lines.Count && picked.Count < size; i++)
                {
                    if (picked.Any(x => x.Product == lines[i].Product))
                        continue;
                    picked.Add(lines[i]);
                    lines.RemoveAt(i);
                    i--;
                }

                AddSale(simulation, date, picked, products, clients[PickWeighted(clientWeights, totalClientWeight)]);
                ordersCount++;
                itemsCount += picked.Count;

                if (++pendingOrders >= OrdersPerBatch)
                {
                    await SaveAndClearAsync(ct);
                    pendingOrders = 0;
                }
            }
        }

        await SaveAndClearAsync(ct);
        return (ordersCount, itemsCount);
    }

    private void AddSale(DemoStoreSimulation simulation, DateOnly date, List<(int Product, int Quantity)> picked, List<Product> products, Client client)
    {
        var createdAt = ToUtc(date, _rng.Next(8, 21), _rng.Next(0, 60));
        var paidAt = createdAt.AddMinutes(_rng.Next(2, 30));

        var order = new Order(new CreateOrderDto { ClientId = client.Id, UserId = _adminId, Type = OrderType.Sale });
        var movements = new List<StockMovement>(picked.Count);
        decimal total = 0;

        foreach (var (p, quantity) in picked)
        {
            var unitPrice = simulation.HistoricalValue(DemoStoreCatalog.Products[p].Price, date);
            var promotion = simulation.Promotions.FirstOrDefault(x => x.Product == p && date >= x.Start && date <= x.End);
            if (promotion != null)
                unitPrice = Math.Round(unitPrice * (1 - promotion.DiscountPercent / 100m), 2, MidpointRounding.AwayFromZero);

            var item = new OrderItem(new CreateOrderItemDto
            {
                OrderId = order.Id,
                ProductId = products[p].Id,
                Quantity = quantity,
                Discount = 0
            });
            item.SetPricing(unitPrice);
            order.OrderItems.Add(item);
            total += unitPrice * quantity;

            // Mesmo registro que o CreateOrderUseCase gera para uma venda
            movements.Add(new StockMovement(new CreateStockMovementDto
            {
                ProductId = products[p].Id,
                Quantity = quantity,
                MovementType = MovementType.Sales,
                Description = $"Movimentação de estoque para o pedido {order.Id}",
                UserId = _adminId,
                Quote = unitPrice
            }));
        }

        order.SetTotals(total, 0);
        order.UpdateStatus(OrderStatus.PaymentConfirmed, "Venda finalizada");

        var sale = new Sale(new CreateSaleDto { UserId = _adminId, OrderId = order.Id });
        var payment = new Payment(new CreatePaymentDto { SaleId = sale.Id, Amount = (double)total, PaymentMethod = PickPaymentMethod() });

        _context.Orders.Add(order); // inclui os OrderItems (grafo)
        _context.Sales.Add(sale);
        _context.Payments.Add(payment);
        _context.StockMovements.AddRange(movements);

        Backdate(order, createdAt);
        _context.Entry(order).Property(x => x.UpdateAt).CurrentValue = paidAt;
        foreach (var item in order.OrderItems)
            Backdate(item, createdAt);
        foreach (var movement in movements)
            Backdate(movement, createdAt);
        Backdate(sale, paidAt);
        Backdate(payment, paidAt);
    }

    // ------------------------------------------------------------------ helpers

    private async Task SaveAndClearAsync(CancellationToken ct)
    {
        await _context.SaveChangesAsync(ct);
        _context.ChangeTracker.Clear();
    }

    private void Backdate<TEntity>(TEntity entity, DateTime createAtUtc) where TEntity : BaseModel
        => _context.Entry(entity).Property(x => x.CreateAt).CurrentValue = createAtUtc;

    private static DateTime ToUtc(DateOnly date, int hour, int minute)
        => BusinessClock.ToUtc(date, new TimeOnly(hour, minute));

    private int PickOrderSize()
    {
        var roll = _rng.Next(100);
        return roll < 30 ? 1 : roll < 65 ? 2 : roll < 87 ? 3 : 4;
    }

    private int PickWeighted(int[] weights, int totalWeight)
    {
        var roll = _rng.Next(totalWeight);
        for (var i = 0; i < weights.Length; i++)
        {
            if (roll < weights[i])
                return i;
            roll -= weights[i];
        }
        return weights.Length - 1;
    }

    private PaymentMethod PickPaymentMethod()
    {
        var roll = _rng.Next(100);
        return roll < 40 ? PaymentMethod.Pix
            : roll < 70 ? PaymentMethod.CreditCard
            : roll < 85 ? PaymentMethod.DebitCard
            : roll < 95 ? PaymentMethod.Cash
            : PaymentMethod.BankTransfer;
    }

    private void Shuffle<T>(List<T> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = _rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private static string OnlyDigits(string value) => new([.. value.Where(char.IsDigit)]);

    /// <summary>CPF válido (dígitos verificadores calculados), apenas números.</summary>
    private string GenerateCpf()
    {
        int[] digits;
        do
        {
            digits = [.. Enumerable.Range(0, 9).Select(_ => _rng.Next(0, 10))];
        } while (digits.All(x => x == digits[0]));

        var d1 = CheckDigit(digits, [10, 9, 8, 7, 6, 5, 4, 3, 2]);
        var d2 = CheckDigit([.. digits, d1], [11, 10, 9, 8, 7, 6, 5, 4, 3, 2]);
        return string.Concat(digits) + d1 + d2;
    }

    /// <summary>CNPJ válido (matriz 0001, dígitos verificadores calculados), apenas números.</summary>
    private string GenerateCnpj()
    {
        int[] digits;
        do
        {
            digits = [.. Enumerable.Range(0, 8).Select(_ => _rng.Next(0, 10)), 0, 0, 0, 1];
        } while (digits.Take(8).All(x => x == digits[0]));

        var d1 = CheckDigit(digits, [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]);
        var d2 = CheckDigit([.. digits, d1], [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]);
        return string.Concat(digits) + d1 + d2;
    }

    private static int CheckDigit(int[] digits, int[] weights)
    {
        var sum = 0;
        for (var i = 0; i < weights.Length; i++)
            sum += digits[i] * weights[i];
        var rest = sum % 11;
        return rest < 2 ? 0 : 11 - rest;
    }
}
