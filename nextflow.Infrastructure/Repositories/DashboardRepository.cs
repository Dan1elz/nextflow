using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Interfaces.Repositories;
using Nextflow.Infrastructure.Database;

namespace Nextflow.Infrastructure.Repositories;

public class DashboardRepository(AppDbContext context) : IDashboardRepository
{
    private readonly AppDbContext _context = context;

    public async Task<DashboardStatsDto> GetStatsAsync(Guid userId, CancellationToken ct)
    {
        // 1. Obter todas as vendas ativas com o TotalAmount do pedido relacionado
        var salesQuery = _context.Sales
            .Where(s => s.IsActive)
            .Select(s => new { s.UserId, TotalAmount = s.Order != null ? s.Order.TotalAmount : 0 });

        var salesList = await salesQuery.ToListAsync(ct);

        var totalSalesRevenue = salesList.Sum(s => s.TotalAmount);
        var totalSalesCount = salesList.Count;
        var avgSaleValue = totalSalesCount > 0 ? totalSalesRevenue / totalSalesCount : 0;

        // 2. Calcular Vendas do Usuário Logado
        var mySalesList = salesList.Where(s => s.UserId == userId).ToList();
        var mySalesRevenue = mySalesList.Sum(s => s.TotalAmount);
        var mySalesCount = mySalesList.Count;
        var myAvgSaleValue = mySalesCount > 0 ? mySalesRevenue / mySalesCount : 0;

        // 3. Histórico de Compras Recentes (Últimos 5 Pedidos de Compra)
        var recentPurchases = await _context.PurchaseOrders
            .Where(po => po.Active)
            .OrderByDescending(po => po.CreateAt)
            .Take(5)
            .Select(po => new RecentPurchaseDto
            {
                Id = po.Id,
                SupplierName = po.Supplier != null ? po.Supplier.Name : "Desconhecido",
                TotalAmount = po.TotalAmount,
                Status = po.PurchaseStatus.ToString(),
                Date = po.CreateAt
            })
            .ToListAsync(ct);

        // 4. Alertas de Estoque (abaixo do estoque mínimo do produto; sem mínimo definido, abaixo de 15 unidades)
        var stockAlerts = await _context.Products
            .Where(p => p.IsActive && p.Quantity < (p.MinStock > 0 ? p.MinStock : 15))
            .OrderBy(p => p.Quantity)
            .Take(5)
            .Select(p => new StockAlertDto
            {
                ProductId = p.Id,
                ProductName = p.Name,
                ProductCode = p.ProductCode,
                CurrentStock = p.Quantity,
                MinStock = p.MinStock,
                UnitType = p.UnitType.ToString()
            })
            .ToListAsync(ct);

        // 5. Dados do Gráfico (Últimos 7 dias)
        var today = DateTime.UtcNow.Date;
        var sevenDaysAgo = today.AddDays(-6);

        // Obter vendas agrupadas por dia nos últimos 7 dias
        var recentSales = await _context.Sales
            .Where(s => s.IsActive && s.CreateAt >= sevenDaysAgo)
            .Select(s => new
            {
                Date = s.CreateAt.Date,
                Amount = s.Order != null ? s.Order.TotalAmount : 0,
                IsMySale = s.UserId == userId
            })
            .ToListAsync(ct);

        var salesChartData = new List<SalesChartPointDto>();
        for (int i = 6; i >= 0; i--)
        {
            var targetDate = today.AddDays(-i);
            var dateStr = targetDate.ToString("dd/MM");
            var daySales = recentSales.Where(s => s.Date.Date == targetDate.Date).ToList();

            salesChartData.Add(new SalesChartPointDto
            {
                DateLabel = dateStr,
                TotalSales = daySales.Sum(s => s.Amount),
                MySales = daySales.Where(s => s.IsMySale).Sum(s => s.Amount)
            });
        }

        return new DashboardStatsDto
        {
            TotalSalesRevenue = totalSalesRevenue,
            TotalSalesCount = totalSalesCount,
            AvgSaleValue = avgSaleValue,
            MySalesRevenue = mySalesRevenue,
            MySalesCount = mySalesCount,
            MyAvgSaleValue = myAvgSaleValue,
            RecentPurchases = recentPurchases,
            StockAlerts = stockAlerts,
            SalesChartData = salesChartData
        };
    }
}
