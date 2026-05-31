using System;
using System.Collections.Generic;

namespace Nextflow.Domain.Dtos;

public class DashboardStatsDto
{
    public decimal TotalSalesRevenue { get; set; }
    public int TotalSalesCount { get; set; }
    public decimal AvgSaleValue { get; set; }

    public decimal MySalesRevenue { get; set; }
    public int MySalesCount { get; set; }
    public decimal MyAvgSaleValue { get; set; }

    public List<RecentPurchaseDto> RecentPurchases { get; set; } = [];
    public List<StockAlertDto> StockAlerts { get; set; } = [];
    public List<SalesChartPointDto> SalesChartData { get; set; } = [];
}

public class RecentPurchaseDto
{
    public Guid Id { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime Date { get; set; }
}

public class StockAlertDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public decimal CurrentStock { get; set; }
    public string UnitType { get; set; } = string.Empty;
}

public class SalesChartPointDto
{
    public string DateLabel { get; set; } = string.Empty;
    public decimal TotalSales { get; set; }
    public decimal MySales { get; set; }
}
