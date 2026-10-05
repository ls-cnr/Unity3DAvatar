using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class EngagementManager : MonoBehaviour
{
    [Header("Configuration")]
    public ScriptableObject policyAsset;
    public AvatarHeadController avatarController;

    private IEngagementPolicy _policy;
    private EngagementCalculator _calculator;
    private BTNode _treeRoot;
    private List<ParticipantContext> _participants = new List<ParticipantContext>();
    private Faces.TrackedFace _currentEngagedFace = null;

    void Awake()
    {
        _policy = (IEngagementPolicy)policyAsset;
        _calculator = new EngagementCalculator(_policy);
        BuildAttentionTree();
    }

    private void BuildAttentionTree()
    {
        // Branch 1: TargetUntracking
        var untrackSequence = new SequenceNode(new List<BTNode> {
            new CheckHasCurrentTargetLeaf(this),
            new CheckUntrackPolicyLeaf(_policy, this),
            new UntrackTargetLeaf(this)
        });
        var untrackDecorator = new SuccessfulUntrackDecorator(untrackSequence);

        // Branch 2: TargetMaintenance
        var maintenanceSequence = new SequenceNode(new List<BTNode> {
            new CheckCandidateIsDifferentLeaf(this),
            new CheckCandidatePriorityLeaf(this),
            new CheckChangePolicyLeaf(_policy, this),
            new ChangeTargetLeaf(this)
        });
        var maintenanceDecorator = new CurrentTargetNotEmptyDecorator(maintenanceSequence, this);

        // Branch 3: TargetTracking
        var trackingSequence = new SequenceNode(new List<BTNode> {
            new CheckCandidatePolicyLeaf(_policy, this),
            new TrackCandidateLeaf(this)
        });

        // Root Selector
        _treeRoot = new SelectorNode(new List<BTNode> { 
            untrackDecorator, 
            maintenanceDecorator, 
            trackingSequence 
        });
    }

    // FIXED: Accept camera dimensions
    public void UpdateEngagement(List<Faces.TrackedFace> activeFaces, float deltaTime, int camWidth, int camHeight)
    {
        SyncParticipants(activeFaces, deltaTime);

        // FIXED: Pass true camera resolution to the calculator
        foreach (var p in _participants)
        {
            VariableCalculator.UpdateVariables(p, deltaTime, _participants, camWidth, camHeight);
        }

        _calculator.Calculate(_participants);
        _treeRoot.Evaluate();
    }

    private void SyncParticipants(List<Faces.TrackedFace> faces, float dt)
    {
        _participants.RemoveAll(p => !faces.Contains(p.FaceData));
        foreach (var f in faces) {
            if (!_participants.Any(p => p.FaceData == f)) _participants.Add(new ParticipantContext(f));
        }
    }

    // --- API for BT Leaves ---
    public ParticipantContext GetBestCandidate() => _participants.Count > 0 ? _participants.OrderByDescending(p => p.Probability).First() : null;
    public ParticipantContext GetCurrentEngagedFace() => _currentEngagedFace != null ? _participants.FirstOrDefault(p => p.FaceData == _currentEngagedFace) : null;
    
    public void SetEngagedTarget(Faces.TrackedFace face)
    {
        _currentEngagedFace = face;
        if (avatarController != null)
        {
            float normX = face.CenterX / Screen.width; // Avatar control still uses Screen for UI mapping
            float normY = face.CenterY / Screen.height;
            avatarController.LookAtFace(normX, normY);
        }
    }

    public void ClearEngagedTarget()
    {
        _currentEngagedFace = null;
        if (avatarController != null) 
        {
            avatarController.ReturnToCenter();
        }
    }
}