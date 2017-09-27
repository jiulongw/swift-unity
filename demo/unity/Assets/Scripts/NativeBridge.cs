using UnityEngine;

public class NativeBridge : MonoBehaviour
{

    private void RotateCube(string command)
    {
        switch (command)
        {
            case "start":
                CubeController.I.ShouldRotate = true;
                break;
            case "stop":
                CubeController.I.ShouldRotate = false;
                break;
        }
    }
}
