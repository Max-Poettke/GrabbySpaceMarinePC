using UnityEngine;
using System.Collections;
using UnityEngine.Animations.Rigging;

public class ProceduralWalkingDuringAnimation : MonoBehaviour
{
    [SerializeField] Transform leftCastOrigin;
    [SerializeField] Transform rightCastOrigin;
    [SerializeField] Transform leftHandIK;
    [SerializeField] Transform rightHandIK;
    [SerializeField] Rig armIKRig;
    [SerializeField] float castRadius = 0.1f;

    public void StartIK(){
        armIKRig.weight = 1f;
        LFindSuitableHandRestPosition();
        RFindSuitableHandRestPosition();
    }

    public void StopIK(){
        armIKRig.weight = 0f;
    }

    public void LFindSuitableHandRestPosition(){
        RaycastHit hit;
        if(Physics.SphereCast(leftCastOrigin.position, castRadius, Vector3.down, out hit, 1.5f)){
            leftHandIK.position = hit.point;
            leftHandIK.forward = transform.forward;
            leftHandIK.rotation = Quaternion.FromToRotation(leftHandIK.up, hit.normal) * leftHandIK.rotation;
        } else {
            return;
        }
    }

    public void RFindSuitableHandRestPosition(){
        RaycastHit hit;
        if(Physics.SphereCast(rightCastOrigin.position, castRadius, Vector3.down, out hit, 1.5f)){
            rightHandIK.position = hit.point;
            rightHandIK.forward = transform.forward;
            rightHandIK.rotation = Quaternion.FromToRotation(rightHandIK.up, hit.normal) * rightHandIK.rotation;
        } else {
            return;
        }
    }
}
