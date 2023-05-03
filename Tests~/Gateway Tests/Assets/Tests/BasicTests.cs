using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

using MuxyGateway;

public class NewTestScript
{
    // A UnityTest behaves like a coroutine in Play Mode. In Edit Mode you can use
    // `yield return null;` to skip a frame.
    [UnityTest]
    public IEnumerator NewTestScriptWithEnumeratorPasses()
    {
        SDK sdk = new SDK("MY Game");
        Assert.IsFalse(sdk.IsAuthenticated);

        sdk.StopWebsocketTransport();

        yield return null;
    }
}
