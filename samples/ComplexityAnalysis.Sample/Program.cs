using System;
using System.Collections.Generic;

namespace ComplexityAnalysis.Sample;

internal static class Program
{
    private static void Main()
    {
        List<Order> orders =
        [
            new(1001, 7, 42m, OrderStatus.New),
            new(1002, 9, 128m, OrderStatus.Ready),
            new(1003, 12, 8m, OrderStatus.Hold),
        ];
        List<int> orderCustomerIds = [7, 9, 12];
        List<int> blockedCustomerIds = [7, 22, 35, 48];

        int blockedOrders = CountBlockedCustomers(orderCustomerIds, blockedCustomerIds);
        string routingBucket = ClassifyOrder(orders[0]);

        Console.WriteLine($"Blocked orders: {blockedOrders}");
        Console.WriteLine($"Routing bucket: {routingBucket}");
    }

    private static int CountBlockedCustomers(List<int> customerIds, List<int> blockedCustomerIds)
    {
        int count = 0;

        foreach (int customerId in customerIds)
        {
            if (blockedCustomerIds.Contains(customerId))
            {
                count++;
            }
        }

        return count;
    }

    private static string ClassifyOrder(Order order)
    {
        if (order.Total <= 0m)
        {
            return "invalid";
        }

        if (order.Status == OrderStatus.Hold)
        {
            return "manual-review";
        }

        if (order.Status == OrderStatus.Ready && order.Total > 100m)
        {
            return "priority";
        }

        if (order.CustomerId % 2 == 0)
        {
            return "batch-a";
        }

        return "batch-b";
    }

    private sealed record Order(int Id, int CustomerId, decimal Total, OrderStatus Status);

    private enum OrderStatus
    {
        New,
        Hold,
        Ready,
    }
}
