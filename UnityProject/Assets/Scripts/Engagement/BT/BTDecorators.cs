using UnityEngine;

/// <summary>
/// SuccessfulUntrackDecorator:
/// If the child (TargetUntracking) returns Success, this decorator returns Failure.
/// WHY: If we successfully untracked, we want the Selector to fall through to the 
/// TargetTracking branch immediately to find a new person. If we returned Success, 
/// the tree would stop and the avatar would look at nothing.
/// </summary>
public class SuccessfulUntrackDecorator : BTNode
{
    private BTNode _child;
    public SuccessfulUntrackDecorator(BTNode child) { _child = child; }

    public override NodeStatus Evaluate()
    {
        NodeStatus status = _child.Evaluate();
        if (status == NodeStatus.Success)
        {
            return NodeStatus.Failure; // Invert to allow fall-through to Tracking
        }
        return status;
    }
}

/// <summary>
/// CurrentTargetNotEmptyDecorator:
/// If the child (TargetMaintenance) fails, but we still have a valid target, 
/// return Success.
/// WHY: This implements "Keep looking at the current person if no one better comes along."
/// </summary>
public class CurrentTargetNotEmptyDecorator : BTNode
{
    private BTNode _child;
    private EngagementManager _manager;
    
    public CurrentTargetNotEmptyDecorator(BTNode child, EngagementManager manager) 
    { 
        _child = child; 
        _manager = manager; 
    }

    public override NodeStatus Evaluate()
    {
        NodeStatus status = _child.Evaluate();
        
        // If maintenance failed (e.g., no better candidate found)
        if (status == NodeStatus.Failure)
        {
            // But we still have someone we are looking at
            if (_manager.GetCurrentEngagedFace() != null)
            {
                return NodeStatus.Success; // Force success to stay on current target
            }
        }
        
        return status;
    }
}