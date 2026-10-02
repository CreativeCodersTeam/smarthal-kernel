using SmartHal.Contracts.DataTypes;
using SmartHal.Contracts.Primitives;

namespace SmartHal.Core.Validation;

/// <summary>
/// Analyses the references between the reusable data types of one catalog: which references close a cycle and how
/// many struct levels each data type spans through its references.
/// </summary>
/// <remarks>
/// <para>
/// The graph is built once per validation run. The strongly connected components are computed with Tarjan's
/// algorithm; two data types reference each other in a cycle exactly when they share a component. The struct levels
/// of a data type are computed once and cached, and references into the data type's own component count no level,
/// so every data type is expanded once and the cost is linear in the size of the catalog.
/// </para>
/// <para>
/// Both computations walk the references with an explicit stack instead of recursion: the catalog comes from outside,
/// and a long chain of references must not exhaust the call stack. Only the nesting inside a single data type is
/// walked recursively, and that is bounded by the JSON depth limit of the serializer.
/// </para>
/// <para>
/// Unresolved references, references without a name and <see langword="null"/> entries add no edge and no level;
/// they are reported where they are defined.
/// </para>
/// </remarks>
internal sealed class ReferenceGraph
{
    private readonly IReadOnlyDictionary<(string Name, int Major), DataTypeDef> _definitions;
    private readonly Dictionary<(string Name, int Major), List<(string Name, int Major)>> _edges = [];
    private readonly Dictionary<(string Name, int Major), int> _component = [];
    private readonly Dictionary<(string Name, int Major), int> _levels = [];

    // State of Tarjan's algorithm.
    private readonly Dictionary<(string Name, int Major), int> _order = [];
    private readonly Dictionary<(string Name, int Major), int> _lowLink = [];
    private readonly Stack<(string Name, int Major)> _stack = new Stack<(string Name, int Major)>();
    private readonly HashSet<(string Name, int Major)> _onStack = [];
    private int _counter;
    private int _componentCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReferenceGraph"/> class.
    /// </summary>
    /// <param name="definitions">The resolvable data types of the catalog, keyed by name and major version.</param>
    public ReferenceGraph(IReadOnlyDictionary<(string Name, int Major), DataTypeDef> definitions)
    {
        _definitions = definitions;

        foreach (var (key, definition) in definitions)
        {
            var targets = new List<(string Name, int Major)>();
            CollectReferences(definition.DataType, targets);
            _edges[key] = targets;
        }

        // A node reached from an earlier start is already numbered. The filter is evaluated lazily per key, so it sees
        // the numbering of every earlier Connect; Connect only reads the edges, so enumerating them is safe.
        foreach (var key in _edges.Keys.Where(key => !_order.ContainsKey(key)))
        {
            Connect(key);
        }
    }

    /// <summary>
    /// Gets how many times the struct levels of a data type were expanded rather than taken from the cache.
    /// </summary>
    /// <value>At most the number of data types of the catalog.</value>
    internal int LevelExpansions { get; private set; }

    /// <summary>
    /// Tests whether a reference inside a data type definition closes a cycle back to that definition.
    /// </summary>
    /// <remarks>
    /// Membership refers to the exact definition the catalog resolves to. A duplicate entry that loses to another
    /// minor version of the same major version is not part of the graph, so its references close no cycle; the
    /// duplicate itself is reported by the catalog validation.
    /// </remarks>
    /// <param name="owner">The definition that contains the reference.</param>
    /// <param name="target">The data type the reference points to.</param>
    /// <returns>
    /// <see langword="true"/> if the owner is the definition the catalog resolves to and the target leads back to it;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public bool ClosesCycle(DataTypeDef owner, TypeRef target)
    {
        if (ValidationContext.IsNull(owner.Name) || ValidationContext.IsNull(target.Name))
        {
            return false;
        }

        var ownerKey = (owner.Name, owner.Version.Major);

        return _definitions.TryGetValue(ownerKey, out var resolved)
            && ReferenceEquals(resolved, owner)
            && _component.TryGetValue(ownerKey, out var ownerComponent)
            && _component.TryGetValue(Key(target), out var targetComponent)
            && ownerComponent == targetComponent;
    }

    /// <summary>
    /// Gets the struct levels a data type spans, following its references outside its own cycle.
    /// </summary>
    /// <param name="target">The data type to measure.</param>
    /// <returns>The number of nested struct levels; 0 for an unresolvable reference.</returns>
    public int Levels(TypeRef target)
    {
        var key = Key(target);

        if (!_component.ContainsKey(key))
        {
            return 0;
        }

        // Post-order over the acyclic graph of components: a data type is measured only after every data type it
        // refers to outside its own component, so measuring it never has to descend into another type.
        var pending = new Stack<((string Name, int Major) Node, bool DependenciesDone)>();
        pending.Push((key, false));

        while (pending.Count > 0)
        {
            var (node, dependenciesDone) = pending.Pop();

            if (_levels.ContainsKey(node))
            {
                continue;
            }

            if (dependenciesDone)
            {
                LevelExpansions++;
                _levels[node] = StructLevels(_definitions[node].DataType, _component[node]);

                continue;
            }

            pending.Push((node, true));

            foreach (var next in _edges[node])
            {
                if (_component[next] != _component[node] && !_levels.ContainsKey(next))
                {
                    pending.Push((next, false));
                }
            }
        }

        return _levels[key];
    }

    private int StructLevels(DataType? dataType, int component) =>
        dataType switch
        {
            ObjectType { Fields: { } fields } => 1 + fields.Values.Select(field => StructLevels(field, component)).DefaultIfEmpty(0).Max(),
            ObjectType => 1,
            ArrayType arrayType => StructLevels(arrayType.Items, component),
            RefType refType => ReferenceLevels(refType.Ref, component),
            _ => 0
        };

    // A reference into the own cycle adds no level: the cycle itself is reported, and counting it would never end.
    // Every other resolvable reference has been measured before, because Levels works in post-order.
    private int ReferenceLevels(TypeRef reference, int component)
    {
        var key = Key(reference);

        return _component.TryGetValue(key, out var targetComponent) && targetComponent != component
            ? _levels.GetValueOrDefault(key)
            : 0;
    }

    private void CollectReferences(DataType? dataType, List<(string Name, int Major)> targets)
    {
        switch (dataType)
        {
            case ObjectType { Fields: { } fields }:
                foreach (var field in fields.Values)
                {
                    CollectReferences(field, targets);
                }

                break;
            case ArrayType arrayType:
                CollectReferences(arrayType.Items, targets);
                break;
            case RefType refType when _definitions.ContainsKey(Key(refType.Ref)):
                targets.Add(Key(refType.Ref));
                break;
            default:
                break;
        }
    }

    // Tarjan's algorithm with an explicit stack: a frame is a node and the index of its next edge to follow.
    private void Connect((string Name, int Major) root)
    {
        var frames = new Stack<((string Name, int Major) Node, int NextEdge)>();
        Open(root);
        frames.Push((root, 0));

        while (frames.Count > 0)
        {
            var (node, nextEdge) = frames.Pop();
            var targets = _edges[node];

            if (nextEdge < targets.Count)
            {
                frames.Push((node, nextEdge + 1));
                var target = targets[nextEdge];

                if (!_order.TryGetValue(target, out var targetOrder))
                {
                    Open(target);
                    frames.Push((target, 0));
                }
                else if (_onStack.Contains(target))
                {
                    _lowLink[node] = Math.Min(_lowLink[node], targetOrder);
                }

                continue;
            }

            // Every edge of the node is done: hand its low link to the node it was reached from.
            if (frames.TryPeek(out var parent))
            {
                _lowLink[parent.Node] = Math.Min(_lowLink[parent.Node], _lowLink[node]);
            }

            if (_lowLink[node] == _order[node])
            {
                CloseComponent(node);
            }
        }
    }

    private void Open((string Name, int Major) node)
    {
        _order[node] = _counter;
        _lowLink[node] = _counter;
        _counter++;
        _stack.Push(node);
        _onStack.Add(node);
    }

    private void CloseComponent((string Name, int Major) root)
    {
        var component = _componentCount++;
        (string Name, int Major) member;

        do
        {
            member = _stack.Pop();
            _onStack.Remove(member);
            _component[member] = component;
        }
        while (member != root);
    }

    private static (string Name, int Major) Key(TypeRef reference) => (reference.Name, reference.Major);
}
