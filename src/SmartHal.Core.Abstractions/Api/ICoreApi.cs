using SmartHal.Contracts.Addressing;
using SmartHal.Contracts.Api;
using SmartHal.Contracts.Runtime;
using SmartHal.Contracts.Topology;

namespace SmartHal.Core.Abstractions.Api;

/// <summary>
/// Defines the operations through which automations, scenes, the UI and external tools talk to the kernel.
/// </summary>
/// <remarks>
/// <para>
/// The interface is transport-neutral; REST, WebSocket or MQTT mappings sit on top of it. Addresses may use UUIDs or
/// keys, and the kernel resolves keys when it is called. Every filter has the same shape.
/// </para>
/// <para>
/// Alarms are acknowledged and shelved through <see cref="InvokeAsync"/> on <c>core.alarms</c>; there is no
/// operation of its own for that.
/// </para>
/// </remarks>
public interface ICoreApi
{
    /// <summary>
    /// Lists the devices a filter selects, with their channels and capabilities.
    /// </summary>
    /// <param name="filter">The filter that selects the devices; <see langword="null"/> selects every device.</param>
    /// <param name="ct">A token that cancels the operation.</param>
    /// <returns>The selected devices.</returns>
    Task<IReadOnlyList<Device>> ListDevicesAsync(Filter? filter = null, CancellationToken ct = default);

    /// <summary>
    /// Gets one device by its id or by its key.
    /// </summary>
    /// <param name="idOrKey">The UUID or the key of the device; a former key that is still valid as an alias is accepted.</param>
    /// <param name="ct">A token that cancels the operation.</param>
    /// <returns>The device with its channels and capabilities.</returns>
    Task<Device> GetDeviceAsync(string idOrKey, CancellationToken ct = default);

    /// <summary>
    /// Gets every schema type the kernel knows.
    /// </summary>
    /// <param name="ct">A token that cancels the operation.</param>
    /// <returns>The type catalog.</returns>
    Task<TypeCatalog> GetTypesAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the current property states a filter selects.
    /// </summary>
    /// <param name="filter">The filter that selects the properties.</param>
    /// <param name="ct">A token that cancels the operation.</param>
    /// <returns>The current states of the selected properties.</returns>
    Task<IReadOnlyList<PropertyState>> GetStateAsync(Filter filter, CancellationToken ct = default);

    /// <summary>
    /// Gets the time series of one property.
    /// </summary>
    /// <param name="address">The address of the property.</param>
    /// <param name="periodStart">The start of the requested period.</param>
    /// <param name="periodEnd">The end of the requested period.</param>
    /// <param name="rollup">The interval of the requested rollup; <see langword="null"/> for raw values.</param>
    /// <param name="ct">A token that cancels the operation.</param>
    /// <returns>The points of the time series in the period.</returns>
    Task<IReadOnlyList<HistoryPoint>> GetHistoryAsync(
        ElementRef address,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        TimeSpan? rollup = null,
        CancellationToken ct = default);

    /// <summary>
    /// Lists the alarm instances a filter selects.
    /// </summary>
    /// <param name="filter">The filter that selects the alarms; <see langword="null"/> selects every alarm.</param>
    /// <param name="states">The alarm states to select; <see langword="null"/> selects every state.</param>
    /// <param name="ct">A token that cancels the operation.</param>
    /// <returns>The selected alarm instances.</returns>
    Task<IReadOnlyList<AlarmInstance>> ListAlarmsAsync(
        Filter? filter = null,
        IReadOnlyList<AlarmState>? states = null,
        CancellationToken ct = default);

    /// <summary>
    /// Subscribes to the changes a filter selects: first a snapshot, then the bus messages, each with a cursor.
    /// </summary>
    /// <remarks>
    /// With <paramref name="since"/> the kernel replays what was missed while it is still buffered; otherwise it sends
    /// a resync item and a new snapshot. The subscription ends when <paramref name="ct"/> is cancelled.
    /// </remarks>
    /// <param name="filter">The filter that selects the changes.</param>
    /// <param name="since">The cursor to resume from; <see langword="null"/> to start with a snapshot.</param>
    /// <param name="ct">A token that ends the subscription.</param>
    /// <returns>The stream of subscription items.</returns>
    IAsyncEnumerable<SubscriptionItem> SubscribeAsync(Filter filter, string? since = null, CancellationToken ct = default);

    /// <summary>
    /// Invokes a command.
    /// </summary>
    /// <remarks>
    /// A request with an idempotency key that was already used within the time window returns the existing
    /// invocation instead of creating a new one.
    /// </remarks>
    /// <param name="request">The invocation request.</param>
    /// <param name="ct">A token that cancels the operation.</param>
    /// <returns>The invocation in its current status.</returns>
    Task<CommandInvocation> InvokeAsync(InvokeRequest request, CancellationToken ct = default);

    /// <summary>
    /// Gets a command invocation by its id.
    /// </summary>
    /// <param name="id">The id of the invocation, which is also its correlation id.</param>
    /// <param name="ct">A token that cancels the operation.</param>
    /// <returns>The invocation in its current status.</returns>
    Task<CommandInvocation> GetInvocationAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Cancels a command invocation that is still pending.
    /// </summary>
    /// <remarks>
    /// Cancelling works only while the invocation is pending; it then ends as cancelled.
    /// </remarks>
    /// <param name="id">The id of the invocation.</param>
    /// <param name="ct">A token that cancels the operation.</param>
    /// <returns>The invocation in its status after the attempt.</returns>
    Task<CommandInvocation> CancelAsync(Guid id, CancellationToken ct = default);
}
