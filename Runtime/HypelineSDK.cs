namespace MuxyHypeline
{
    /// <summary>
    /// Hypeline entry point for the existing Muxy Gateway SDK. It uses the same
    /// transport, backend protocol, and public data types as MuxyGateway.SDK.
    /// </summary>
    public class SDK : MuxyGateway.SDK
    {
        public SDK(string gameID) : base(gameID) { }
    }
}
