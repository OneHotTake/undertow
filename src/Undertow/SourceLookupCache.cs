using System.Collections.Concurrent;
using System.Text.Json;

namespace Jellyembifier;

public sealed record SourceLookupResponse(JsonElement Value, DateTimeOffset Checked);

// Short-lived, bounded memory only. Coalesce detail/play/retry calls for opted-in recent movies.
public sealed class SourceLookupCache(Func<DateTimeOffset>? clock = null)
{
    private readonly ConcurrentDictionary<string, SourceLookupResponse> responses = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim[] gates = Enumerable.Range(0, 64).Select(_ => new SemaphoreSlim(1, 1)).ToArray();
    private DateTimeOffset Now => clock?.Invoke() ?? DateTimeOffset.UtcNow;
    public async Task<SourceLookupResponse> Get(string id, bool cache, Func<CancellationToken, Task<JsonElement>> fetch, CancellationToken ct)
    {
        var gate = gates[(StringComparer.Ordinal.GetHashCode(id) & int.MaxValue) % gates.Length];
        await gate.WaitAsync(ct);
        try
        {
            if (cache && responses.TryGetValue(id, out var previous) && Now - previous.Checked < TimeSpan.FromMinutes(1)) return previous;
            var value = await fetch(ct);
            if (value.GetProperty("MediaSources").ValueKind != JsonValueKind.Array) throw new JsonException("Invalid playback source response.");
            var response = new SourceLookupResponse(value.Clone(), Now);
            if (cache)
            {
                foreach (var old in responses.Where(x => Now - x.Value.Checked >= TimeSpan.FromMinutes(1))) responses.TryRemove(old.Key, out _);
                if (responses.Count >= 128) responses.TryRemove(responses.OrderBy(x => x.Value.Checked).First().Key, out _);
                responses[id] = response;
            }
            return response;
        }
        finally { gate.Release(); }
    }
}
