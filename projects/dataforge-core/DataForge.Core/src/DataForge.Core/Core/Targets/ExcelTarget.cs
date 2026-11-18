using DataForge.Core.Core.Infrastructure;
using DataForge.Core.Core.Models;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace DataForge.Core.Core.Targets;

/// <summary>
/// 简单的 Excel 目标（基本实现），使用 CSV 格式
/// 完整功能需要安装 DataForge.Core.Excel 包
/// </summary>
internal class ExcelTarget<T> : IDataTarget<T>
{
    private readonly ExcelExportOptions _options;

    public string Name => "Excel Target";
    public DataTargetType TargetType => DataTargetType.Excel;

    public ExcelTarget(ExcelExportOptions options)
    {
        _options = options;
    }

    public async Task<ExportResults> ExportAsync(IAsyncEnumerable<T> data, string destination, CancellationToken cancellationToken = default)
    {
        var items = new List<T>();
        await foreach (var item in data.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            items.Add(item);
        }

        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream, leaveOpen: true);

        if (_options.IncludeHeader)
        {
            var headers = properties.Select(p => p.Name);
            await writer.WriteLineAsync(string.Join(",", headers)).ConfigureAwait(false);
        }

        foreach (var item in items)
        {
            var values = properties.Select(p => p.GetValue(item)?.ToString() ?? string.Empty);
            await writer.WriteLineAsync(string.Join(",", values)).ConfigureAwait(false);
        }

        await writer.FlushAsync().ConfigureAwait(false);
        await File.WriteAllBytesAsync(destination, stream.ToArray(), cancellationToken).ConfigureAwait(false);

        return new ExportResults
        {
            RecordsWritten = items.Count,
            OutputPath = destination,
            OutputSize = new FileInfo(destination).Length
        };
    }

    public async Task WriteAsync(T item, CancellationToken cancellationToken = default)
    {
        await ExportAsync(ToAsyncEnumerable(item), "", cancellationToken).ConfigureAwait(false);
    }

    public async Task<WriteResult> WriteBatchAsync(IEnumerable<T> items, CancellationToken cancellationToken = default)
    {
        var result = await ExportAsync(ToAsyncEnumerable(items), "", cancellationToken).ConfigureAwait(false);
        return new WriteResult { SuccessCount = result.RecordsWritten };
    }

    public Task CompleteAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    private static async IAsyncEnumerable<T> ToAsyncEnumerable(T item)
    {
        yield return item;
    }

    private static async IAsyncEnumerable<T> ToAsyncEnumerable(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            yield return item;
        }
    }
}
