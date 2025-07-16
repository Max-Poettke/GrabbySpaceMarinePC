using UnityEngine;
using System.Collections;
public class HandAnimationTest : MonoBehaviour
{
    [SerializeField] private Transform leftHand;
    [SerializeField] private Transform rightHand;
    [SerializeField] private float maxDistance = .5f;
    [SerializeField] private float speed = 1f;
    [SerializeField] private Transform targetLeftHand;
    [SerializeField] private Transform targetRightHand;
    Vector3 currentLeftTarget;
    Vector3 currentRightTarget;
    private bool isMovingLeft = false;
    private bool isMovingRight = false;

    private void Start()
    {
        currentLeftTarget = leftHand.position;
        currentRightTarget = rightHand.position;
    }

    // Update is called once per frame
    void Update()
    {
        if(!isMovingLeft)
        {
            if(isMovingRight) return;
            if(Vector3.Distance(currentLeftTarget, targetLeftHand.position) > maxDistance)
            {
                isMovingLeft = true;
                currentLeftTarget = targetLeftHand.position;
                StartCoroutine(MoveHand(leftHand, currentLeftTarget));
            }
        }
        if(!isMovingRight)
        {
            if(isMovingLeft) return;
            if(Vector3.Distance(currentRightTarget, targetRightHand.position) > maxDistance)
            {
                isMovingRight = true;
                currentRightTarget = targetRightHand.position;
                StartCoroutine(MoveHand(rightHand, currentRightTarget));
            }
        }


    }

    private IEnumerator MoveHand(Transform hand, Vector3 target)
    {
        while(Vector3.Distance(hand.position, target) > 0.01f)
        {
            hand.position = Vector3.MoveTowards(hand.position, target, speed * Time.deltaTime);
            yield return null;
        }
        isMovingLeft = false;
        isMovingRight = false;
    }

    //visualize radius of distance
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(currentLeftTarget, maxDistance);
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(currentRightTarget, maxDistance);
    }

}
