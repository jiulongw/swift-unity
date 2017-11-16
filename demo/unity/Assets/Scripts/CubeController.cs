using UnityEngine;

public class CubeController : MonoBehaviour
{
    public static CubeController I { get; private set; }

    public bool ShouldRotate { get; set; }

    private Rigidbody rigibody;

    private float torqueTime;

    private Vector3 torque;

    private void Awake()
    {
        I = this;
        rigibody = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        if (ShouldRotate)
        {
            rigibody.angularDrag = 0.05f;
        }
        else
        {
            rigibody.angularDrag = 1f;
        }
    }

    private void FixedUpdate()
    {
        if (ShouldRotate)
        {
            UpdateTorque();
        }
    }

    private void UpdateTorque()
    {
        if (torqueTime <= 0)
        {
            torqueTime = Random.Range(0.5f, 2f);
            torque = Random.insideUnitSphere * Random.Range(1f, 3f);
        }

        torqueTime -= Time.fixedDeltaTime;

        rigibody.AddTorque(torque, ForceMode.Force);
    }
}
