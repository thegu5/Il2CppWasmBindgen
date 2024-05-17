// Not used for now, I attempted to make the json output a tree format so the TS could be nested namespace but that creates inheritance problems

using System.Text.Json.Serialization;

namespace Il2CppTsBindgen;

#region Node data types
[JsonDerivedType(typeof(DataNode))]
[JsonDerivedType(typeof(NamespaceNode))]
[JsonPolymorphic]
public interface INode
{
    public string Name { get; }
}

public class DataNode(string name, Il2CppClass data) : INode
{
    public string Name { get; } = name;
    public Il2CppClass Data { get; } = data;
}

public class NamespaceNode(string name) : INode
{
    public string Name { get; } = name;
    public List<INode> Children { get; } = [];
    
    public void AddChild(INode child)
    {
        Children.Add(child);
    }
    public Dictionary<string, object> ToNestedDictionary()
    {
        var result = new Dictionary<string, object>();
        foreach (var child in Children)
        {
            switch (child)
            {
                case DataNode dataNode:
                    result[dataNode.Name] = dataNode.Data;
                    break;
                case NamespaceNode namespaceNode:
                    result[namespaceNode.Name] = namespaceNode.ToNestedDictionary();
                    break;
            }
        }
        return result;
    }
}
#endregion

public static class TypeTreeBuilder
{
    public static NamespaceNode BuildTree(Dictionary<string, Il2CppClass> dictionary)
    {
        var root = new NamespaceNode("Root");
        foreach (var kvp in dictionary)
        {
            var typeParts = kvp.Key.Split('.');
            var currentNode = root;

            foreach (var part in typeParts)
            {
                if (typeParts.ToList().IndexOf(part) == typeParts.Length - 1)
                {
                    currentNode.AddChild(new DataNode(part, kvp.Value));
                }
                else
                {
                    var existing = currentNode.Children.FirstOrDefault(n => n.Name == part);
                    if (existing is null)
                    {
                        var newNode = new NamespaceNode(part);
                        currentNode.AddChild(newNode);
                        currentNode = newNode;
                    }
                    else
                    {
                        currentNode = (NamespaceNode) existing;
                    }
                }
            }
        }

        return root;
    }
}