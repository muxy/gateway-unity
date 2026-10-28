using System.IO;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.UnityLinker;

namespace MuxyGateway.Editor
{
    // Package link.xml files are not consumed by all supported Unity versions.
    // Register a generated descriptor through the supported build callback.
    public sealed class GatewayLinkerProcessor : IUnityLinkerProcessor
    {
        public int callbackOrder { get { return 0; } }

        public string GenerateAdditionalLinkXmlFile(BuildReport report, UnityLinkerBuildPipelineData data)
        {
            string directory = Path.GetFullPath(Path.Combine("Library", "MuxyGateway"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "link.xml");
            // Mono's TLS path initializes ServicePointManager, which creates
            // configuration hosts/sections through reflection. High stripping
            // otherwise removes ExeConfigurationHost's default constructor.
            File.WriteAllText(path,
                "<linker>\n" +
                "  <assembly fullname=\"System.Configuration\" preserve=\"all\" ignoreIfMissing=\"1\" />\n" +
                "  <assembly fullname=\"System\" ignoreIfMissing=\"1\">\n" +
                "    <type fullname=\"System.Net.Configuration.*\" preserve=\"all\" />\n" +
                "  </assembly>\n" +
                "</linker>\n");
            return path;
        }

#if !UNITY_2021_2_OR_NEWER
        public void OnBeforeRun(BuildReport report, UnityLinkerBuildPipelineData data) { }
        public void OnAfterRun(BuildReport report, UnityLinkerBuildPipelineData data) { }
#endif
    }
}
