using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;

namespace LightSouls.MapGeneration.Tests
{
    public class ProjectConfigurationTests
    {
        [Test]
        public void AndroidApplicationHasLightSoulsIdentity()
        {
            Assert.AreEqual("Light Souls", PlayerSettings.productName);
            Assert.AreEqual("com.unkluco.lightsouls", PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android));
        }
    }
}
