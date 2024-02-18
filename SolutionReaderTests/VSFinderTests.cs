using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FoxNuGet.VSTools.Tests
{
    [TestClass()]
    public class VSFinderTests
    {
        [TestMethod()]
        public void GetExistingVSTest()
        {
            var list = VSIdeFinder.GetExistingVS();
            Assert.IsNotNull(list);
        }
    }
}