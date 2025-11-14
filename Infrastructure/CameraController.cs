using UnityEngine;

public class CameraController : MonoBehaviour
{
    public float moveSpeed = 30f;
    public float zoomSpeed = 1000f;
    public float minZoom = 15f;
    public float maxZoom = 100f;

    void Update()
    {
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");
        Vector3 moveDirection = new Vector3(horizontalInput, 0, verticalInput);
        transform.Translate(moveDirection * moveSpeed * Time.deltaTime, Space.World);

        float scrollInput = Input.GetAxis("Mouse ScrollWheel");
        Vector3 newPosition = transform.position;
        newPosition.y -= scrollInput * zoomSpeed * Time.deltaTime;
        newPosition.y = Mathf.Clamp(newPosition.y, minZoom, maxZoom);
        transform.position = newPosition;
    }
}