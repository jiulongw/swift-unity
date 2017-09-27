using UnityEngine;

public class CubeController : MonoBehaviour
{
    public static CubeController I { get; private set; }

    public bool ShouldRotate { get; set; }

    private void Awake()
    {
        I = this;
    }

    private void Update()
    {
        if (ShouldRotate)
        {
            var angle = 180f * Time.deltaTime;
            transform.Rotate(Vector3.up, angle);
        }
    }
}
