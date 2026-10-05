using System.Collections.Generic;
using UnityEngine;

public enum NodeStatus { Success, Failure, Running }

public abstract class BTNode { public abstract NodeStatus Evaluate(); }

public class SelectorNode : BTNode
{
    private List<BTNode> _children;
    public SelectorNode(List<BTNode> children) { _children = children; }
    public override NodeStatus Evaluate()
    {
        foreach (var child in _children) {
            if (child.Evaluate() == NodeStatus.Success) return NodeStatus.Success;
        }
        return NodeStatus.Failure;
    }
}

public class SequenceNode : BTNode
{
    private List<BTNode> _children;
    public SequenceNode(List<BTNode> children) { _children = children; }
    public override NodeStatus Evaluate()
    {
        foreach (var child in _children) {
            if (child.Evaluate() == NodeStatus.Failure) return NodeStatus.Failure;
        }
        return NodeStatus.Success;
    }
}

public class DecoratorNode : BTNode
{
    private BTNode _child;
    private System.Func<bool> _condition;
    public DecoratorNode(BTNode child, System.Func<bool> condition) { _child = child; _condition = condition; }
    public override NodeStatus Evaluate()
    {
        return _condition() ? _child.Evaluate() : NodeStatus.Failure;
    }
}