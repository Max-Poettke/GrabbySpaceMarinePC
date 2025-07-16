using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float speed = 10f;
    // Update is called once per frame
    void Update()
    {
       //WASD movement from camera view  
       Vector3 direction = (Vector3.forward * Input.GetAxis("Vertical") + Vector3.right * Input.GetAxis("Horizontal")).normalized;
       transform.Translate(direction * speed * Time.deltaTime, Space.World);


        //rotate player to face movement direction
        transform.LookAt(transform.position + direction);
    }
}
