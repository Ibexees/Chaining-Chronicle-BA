using UnityEditor;
namespace sonar
{
    public class ForceSonarMainProject : AssetPostprocessor
    {
        private static string OnGeneratedCSProject(string content)
        {
            const string marker = "<PropertyGroup>";
            int index = content.IndexOf(marker);

            if (index >= 0 && !content.Contains("SonarQubeTestProject"))
            {
                content = content.Insert(index + marker.Length,
                    "\n    <SonarQubeTestProject>false</SonarQubeTestProject>");
            }

            return content;
        }
    }
}