using UnityEngine;
using System.Collections;

public class MousePositionDisplay : MonoBehaviour
{
    [SerializeField] private Material mousePositionMaterial;
    [SerializeField] private Transform restrictionSphereCenter;
    [SerializeField] private float restrictionSphereRadius;
    public static Vector3 hitPointPosition = Vector3.zero;
    // Update is called once per frame
    void Update()
    {
        RaycastHit hit;
        //raycast mouseposition through the world and set transform position to the point of impact
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if(Physics.Raycast(ray, out hit))
        {
            if(!hit.transform.CompareTag("Climbable")){
                hitPointPosition = Vector3.zero;
                return;
            }
            if(Vector3.Distance(hit.point, restrictionSphereCenter.position) < restrictionSphereRadius)
            {
                transform.position = hit.point;
            } else {
                transform.position = restrictionSphereCenter.position + (hit.point - restrictionSphereCenter.position).normalized * restrictionSphereRadius;
            }
            hitPointPosition = transform.position;
        }
    }
}
