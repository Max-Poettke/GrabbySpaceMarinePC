using UnityEngine;

// When transform gets close to a surface, calculate difference between transform's up and world up and rotate transform to align with surface
public class RotateToSurfaceNormal : MonoBehaviour
{
    public float distanceToSurface = 0.1f;
    public float rotationSpeed = 10f;
    private Vector3 targetRotation;
    private Vector3 currentUp = new Vector3(0, 1, 0);

    //we want to first get the surface normal in world space and relate it to the current up, as the hands back will always be facing the world up before rotating.
    //then we want to rotate the transform by the difference between the current up and the found surface normal. We do the same to the current up to keep track of the rotation.
    //When there is no surface normal to be found, we want to rotate the transform back to its original rotation
    
    private void Update()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position, -Vector3.up, out hit, distanceToSurface))
        {
            RotateToNormal(hit.normal);
        }
    }

    private void RotateToNormal(Vector3 surfaceNormal)
    {
        Vector3 difference = currentUp - surfaceNormal;
        transform.Rotate(difference);
        currentUp = surfaceNormal;
    }
}
