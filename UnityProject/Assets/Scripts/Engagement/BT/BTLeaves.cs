using UnityEngine;

// =========================================================================
// TARGET UNTRACKING BRANCH
// =========================================================================

/// <summary>
/// checkCurrentTargetID (Untrack Branch):
/// Ensures we actually have a target to untrack.
/// </summary>
public class CheckHasCurrentTargetLeaf : BTNode
{
    private EngagementManager _manager;
    public CheckHasCurrentTargetLeaf(EngagementManager m) { _manager = m; }

    public override NodeStatus Evaluate()
    {
        return _manager.GetCurrentEngagedFace() != null ? NodeStatus.Success : NodeStatus.Failure;
    }
}

/// <summary>
/// checkPolicyCondition(s)StillSatisfied:
/// Returns Success if the policy says we should STOP tracking (conditions no longer met).
/// </summary>
public class CheckUntrackPolicyLeaf : BTNode
{
    private IEngagementPolicy _policy;
    private EngagementManager _manager;
    public CheckUntrackPolicyLeaf(IEngagementPolicy p, EngagementManager m) { _policy = p; _manager = m; }

    public override NodeStatus Evaluate()
    {
        var current = _manager.GetCurrentEngagedFace();
        if (current != null && _policy.CheckUntrackCondition(current))
        {
            return NodeStatus.Success; // Policy says: Stop tracking!
        }
        return NodeStatus.Failure;
    }
}

public class UntrackTargetLeaf : BTNode
{
    private EngagementManager _manager;
    public UntrackTargetLeaf(EngagementManager m) { _manager = m; }

    public override NodeStatus Evaluate()
    {
        _manager.ClearEngagedTarget();
        return NodeStatus.Success;
    }
}

// =========================================================================
// TARGET MAINTENANCE BRANCH
// =========================================================================

/// <summary>
/// checkCurrentTargetID (Maintenance Branch):
/// Returns Success if the Best Candidate is DIFFERENT from the Current Target.
/// (i.e., there is someone else we might want to switch to).
/// </summary>
public class CheckCandidateIsDifferentLeaf : BTNode
{
    private EngagementManager _manager;
    public CheckCandidateIsDifferentLeaf(EngagementManager m) { _manager = m; }

    public override NodeStatus Evaluate()
    {
        var current = _manager.GetCurrentEngagedFace();
        var best = _manager.GetBestCandidate();

        if (current != null && best != null && current.FaceData.Id != best.FaceData.Id)
        {
            return NodeStatus.Success;
        }
        return NodeStatus.Failure;
    }
}

/// <summary>
/// checkCurrentCandidatePriorityIsEqualToOrHigherThanCurrentTargetOne:
/// Returns Success if the Best Candidate has a higher probability than the Current Target.
/// </summary>
public class CheckCandidatePriorityLeaf : BTNode
{
    private EngagementManager _manager;
    public CheckCandidatePriorityLeaf(EngagementManager m) { _manager = m; }

    public override NodeStatus Evaluate()
    {
        var current = _manager.GetCurrentEngagedFace();
        var best = _manager.GetBestCandidate();

        if (current != null && best != null && best.Probability >= current.Probability)
        {
            return NodeStatus.Success;
        }
        return NodeStatus.Failure;
    }
}

/// <summary>
/// checkPolicyTargetChangeCondition(s)AreMet:
/// Policy-specific check for switching.
/// </summary>
public class CheckChangePolicyLeaf : BTNode
{
    private IEngagementPolicy _policy;
    private EngagementManager _manager;
    public CheckChangePolicyLeaf(IEngagementPolicy p, EngagementManager m) { _policy = p; _manager = m; }

    public override NodeStatus Evaluate()
    {
        var best = _manager.GetBestCandidate();
        // In Generic Policy, this might just return true if priority is higher
        if (best != null && _policy.CheckChangeCondition(best)) 
        {
            return NodeStatus.Success;
        }
        return NodeStatus.Failure;
    }
}

public class ChangeTargetLeaf : BTNode
{
    private EngagementManager _manager;
    public ChangeTargetLeaf(EngagementManager m) { _manager = m; }

    public override NodeStatus Evaluate()
    {
        var best = _manager.GetBestCandidate();
        if (best != null)
        {
            _manager.SetEngagedTarget(best.FaceData);
            return NodeStatus.Success;
        }
        return NodeStatus.Failure;
    }
}

// =========================================================================
// TARGET TRACKING BRANCH
// =========================================================================

/// <summary>
/// checkCandidateMeetsPolicyCondition(s):
/// Checks if the best candidate is valid to track.
/// </summary>
public class CheckCandidatePolicyLeaf : BTNode
{
    private IEngagementPolicy _policy;
    private EngagementManager _manager;
    public CheckCandidatePolicyLeaf(IEngagementPolicy p, EngagementManager m) { _policy = p; _manager = m; }

    public override NodeStatus Evaluate()
    {
        var best = _manager.GetBestCandidate();
        if (best != null && _policy.CheckEngagementCondition(best))
        {
            return NodeStatus.Success;
        }
        return NodeStatus.Failure;
    }
}

public class TrackCandidateLeaf : BTNode
{
    private EngagementManager _manager;
    public TrackCandidateLeaf(EngagementManager m) { _manager = m; }

    public override NodeStatus Evaluate()
    {
        var best = _manager.GetBestCandidate();
        if (best != null)
        {
            _manager.SetEngagedTarget(best.FaceData);
            return NodeStatus.Success;
        }
        return NodeStatus.Failure;
    }
}

public class MaintainTargetLeaf : BTNode
{
    public override NodeStatus Evaluate()
    {
        return NodeStatus.Success;
    }
}