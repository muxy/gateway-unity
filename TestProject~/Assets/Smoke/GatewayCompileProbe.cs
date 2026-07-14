using MuxyGateway;
using UnityEngine;

public class GatewayCompileProbe : MonoBehaviour
{
    private void Awake()
    {
        MuxyGatewayManager manager = gameObject.AddComponent<MuxyGatewayManager>();
        manager.ConnectionStage = Stage.Sandbox;
    }
}
