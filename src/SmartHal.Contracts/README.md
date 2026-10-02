# SmartHal.Contracts

## Target audience

The BFF team. Everything a client of the SmartHal server needs in order to speak to it is declared
here, and nothing else: this package carries no runtime behaviour and no dependency on any other
SmartHal package.

## Contents

The protocol-neutral IoT domain model and everything that travels between a client and the server:

| Namespace | Contents |
|---|---|
| `SmartHal.Contracts.Primitives` | Type references (`core.pressure@1`), type versions, identity, severity, aggregation |
| `SmartHal.Contracts.DataTypes` | The data types - a subset of JSON Schema - and reusable data type definitions |
| `SmartHal.Contracts.Schema` | Capability types with their properties, commands, events and alarms; channel profiles; device types |
| `SmartHal.Contracts.Topology` | Locations, devices, channels and capability instances |
| `SmartHal.Contracts.Discovery` | Discovery results of the inbox |
| `SmartHal.Contracts.Addressing` | Addresses of capability elements, by UUIDs or by keys |
| `SmartHal.Contracts.Runtime` | Property states and their quality, command invocations, event occurrences, alarm instances |
| `SmartHal.Contracts.Bus` | The three bus messages |
| `SmartHal.Contracts.Integration` | Adapters, connections, bindings, binding templates and mappings, with transforms and virtual-device logic |
| `SmartHal.Contracts.Api` | Filters, history points, invoke requests, subscription items and the type catalog |
| `SmartHal.Contracts.Serialization` | `ContractsJson.Options`, the serializer options every contract is written and read with |

This package carries no runtime behaviour beyond the text forms of its types and their JSON converters.

## Versioning

One version covers the whole repository; it is derived from the git history by GitVersion and is
never maintained by hand. What counts as a breaking change is settled for all three published
packages in [VERSIONING.md](https://github.com/CreativeCodersTeam/smarthal-kernel/blob/main/VERSIONING.md).

## License

Apache-2.0
