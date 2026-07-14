using System.Reflection;
using MuxyGateway;
using NUnit.Framework;
using UnityEngine;

public class GatewayTests
{
    [Test]
    public void ManagerDefaultsToSandbox()
    {
        GameObject gameObject = new GameObject("Gateway test");

        try
        {
            MuxyGatewayManager manager = gameObject.AddComponent<MuxyGatewayManager>();
            Assert.AreEqual(Stage.Sandbox, manager.ConnectionStage);
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }

    [TestCase("gateway.example.test/socket", "wss://gateway.example.test/socket")]
    [TestCase("ws://gateway.example.test/socket", "wss://gateway.example.test/socket")]
    [TestCase("wss://gateway.example.test/socket", "wss://gateway.example.test/socket")]
    public void TransportAlwaysBuildsSecureUris(string address, string expected)
    {
        MethodInfo method = typeof(WebsocketTransport).GetMethod(
            "BuildSecureUri",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.IsNotNull(method);
        Assert.AreEqual(expected, method.Invoke(null, new object[] { address }));
    }
}
