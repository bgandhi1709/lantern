using Azure;
using Azure.Data.Tables;

namespace Lantern.Repository.Entities;

internal abstract class TableEntityBase : ITableEntity
{
    public string PartitionKey { get; set; }

    public string RowKey { get; set; }

    public DateTimeOffset? Timestamp { get; set; }

    public ETag ETag { get; set; }
}
