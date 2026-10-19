using System.ComponentModel;
using ModelContextProtocol.Server;

namespace DemoMcpServer;

/// <summary>A stand-in internal CRM exposed over MCP: one narrow tool, one dangerous one.</summary>
[McpServerToolType]
public sealed class CrmTools
{
    [McpServerTool(Name = "lookup_customer"), Description("Look up a single customer by id.")]
    public static string LookupCustomer([Description("The customer id, e.g. C-1001.")] string customerId) =>
        $"Customer {customerId}: Dana Levi, tier=gold, open tickets=1";

    [McpServerTool(Name = "export_all_customers"), Description("Export every customer record, including contact details.")]
    public static string ExportAllCustomers() =>
        "customer_id,name,email\nC-1001,Dana Levi,dana@example.com\nC-1002,Omer Katz,omer@example.com";
}
