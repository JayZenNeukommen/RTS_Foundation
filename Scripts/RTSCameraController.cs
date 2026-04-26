using UnityEngine;

public class RTSCameraController : MonoBehaviour
{
    public float moveSpeed = 15f;
    public float zoomSpeed = 2500f;
    public float minHeight = 5f;
    public float maxHeight = 25f;
    public static RTSCameraController Instance { get; private set; }

    void Update()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        Vector3 move = new Vector3(horizontal, 0f, vertical).normalized;
        transform.position += move * moveSpeed * Time.deltaTime;

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        Vector3 zoom = transform.forward * scroll * zoomSpeed * Time.deltaTime;
        Vector3 nextPosition = transform.position + zoom;

        if (nextPosition.y >= minHeight && nextPosition.y <= maxHeight)
        {
            transform.position = nextPosition;
        }
    }

    private void Awake()
    {
        Instance = this;
    }

    public void FocusOnWorldPosition(Vector3 worldPosition)
    {
        Vector3 forward = transform.forward;

        if (Mathf.Abs(forward.y) < 0.001f)
        {
            Vector3 simplePosition = transform.position;
            simplePosition.x = worldPosition.x;
            simplePosition.z = worldPosition.z;
            transform.position = simplePosition;
            return;
        }

        float distanceToGround = (transform.position.y - worldPosition.y) / -forward.y;

        Vector3 newCameraPosition = worldPosition - forward * distanceToGround;
        transform.position = newCameraPosition;
    }
}