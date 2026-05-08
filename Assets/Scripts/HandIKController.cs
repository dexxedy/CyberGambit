using UnityEngine;

[RequireComponent(typeof(Animator))]
public class HandIKController : MonoBehaviour
{
    private Animator animator;
    public bool ikActive = true;
    
    [Header("Grips")]
    public Transform leftHandGrip;
    public Transform rightHandGrip;
    
    [Header("Weights")]
    [Range(0, 1)] public float leftHandWeight = 1.0f;
    [Range(0, 1)] public float rightHandWeight = 1.0f;
    
    [Header("FPS Alignment")]
    public bool useFPSAlignment = true;
    public Transform weaponPivot;
    public Vector3 fpsPositionOffset = new Vector3(0.15f, -0.2f, 0.4f);
    public Vector3 fpsRotationOffset = new Vector3(0, 0, 0);

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    private void Start()
    {
        // Try to find weapon and grips if they are already children
        FindGripsInChildren();
    }

    public void FindGripsInChildren()
    {
        HandIKController ik = this;
        // Search in all children for a child named "LeftHandGrip" or "RightHandGrip"
        Transform[] allChildren = GetComponentsInChildren<Transform>(true);
        foreach (var child in allChildren)
        {
            if (child.name == "LeftHandGrip" || child.name == "L_HandGrip") ik.leftHandGrip = child;
            if (child.name == "RightHandGrip" || child.name == "R_HandGrip") ik.rightHandGrip = child;
        }
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (!animator || !ikActive) return;

        // Apply Left Hand IK
        if (leftHandGrip != null)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, leftHandWeight);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, leftHandWeight);
            animator.SetIKPosition(AvatarIKGoal.LeftHand, leftHandGrip.position);
            animator.SetIKRotation(AvatarIKGoal.LeftHand, leftHandGrip.rotation);
        }

        // Apply Right Hand IK
        if (rightHandGrip != null)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, rightHandWeight);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, rightHandWeight);
            animator.SetIKPosition(AvatarIKGoal.RightHand, rightHandGrip.position);
            animator.SetIKRotation(AvatarIKGoal.RightHand, rightHandGrip.rotation);
        }
    }

    public void RefreshGrips(Transform weaponRoot)
    {
        if (weaponRoot == null)
        {
            leftHandGrip = null;
            rightHandGrip = null;
            return;
        }

        leftHandGrip = weaponRoot.Find("LeftHandGrip");
        if (leftHandGrip == null) leftHandGrip = weaponRoot.Find("L_HandGrip");
        
        rightHandGrip = weaponRoot.Find("RightHandGrip");
        if (rightHandGrip == null) rightHandGrip = weaponRoot.Find("R_HandGrip");
    }
}
