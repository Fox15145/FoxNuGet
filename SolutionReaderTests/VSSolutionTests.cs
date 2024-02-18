using Microsoft.VisualStudio.TestTools.UnitTesting;

using FoxNuGet.VSTools;
using NuGet.Versioning;

namespace FoxNuGet.VSSolution.Tests
{
    [TestClass()]
    public class VSSolutionTests
    {
        [TestMethod()]
        public void BadVSSolutionFilenameTest()
        {
            string csprojFilename = @"C:\Users\User1\source\repos\NugetGeneratorDemo\NugetGeneratorDemo\NugetGeneratorDemo.csproj";

            Assert.ThrowsException<InvalidDataException>(() => new VSSolution(new FileInfo(csprojFilename)));
        }

        [TestMethod()]
        public void NotExistsVSSolutionFilenameTest()
        {
            string csprojFilename = @"C:\Users\User1\source\repos\NugetGeneratorDemo\NugetGeneratorDemo\NugetGDemo.sln";

            Assert.ThrowsException<FileNotFoundException>(() => new VSSolution(new FileInfo(csprojFilename)));
        }


        [TestMethod()]
        public void VSSolutionTest()
        {
            VSSolution vSSolution = InitializeVsSolution();

            Assert.IsNotNull(vSSolution);
            Assert.IsTrue(vSSolution.IsProjectsLoaded);

            Assert.IsNotNull(vSSolution.Projects);
            Assert.AreEqual(4, vSSolution.Projects.Count());
            foreach (VSProject project in vSSolution.Projects)
            {
                Assert.IsTrue(project.ProjectFile.Exists);
                Assert.AreNotEqual(string.Empty, project.Name);
                Assert.AreNotEqual(string.Empty, project.UniqueId);
                Assert.AreNotEqual(string.Empty, project.Version.ToString());
                Assert.AreEqual(0, project.ShowWarning().Count());
            }
        }

        private static VSSolution InitializeVsSolution()
        {
            AutoResetEvent autoResetEvent = new(false);
            string solutionFilename = @"C:\Users\User1\source\repos\NugetGeneratorDemo\NugetGeneratorDemo.sln";
            VSSolution vSSolution = new(new FileInfo(solutionFilename));

            vSSolution.OnProjectsLoaded += () =>
            {
                if (vSSolution.IsProjectsLoaded)
                    autoResetEvent.Set();
            };
            autoResetEvent.WaitOne();
            return vSSolution;
        }

        [TestMethod()]
        public void VSSProjectTest()
        {
            AutoResetEvent autoResetEvent = new(false);
            //string solutionFilename = @"C:\Users\User1\source\repos\NugetGeneratorDemo\NugetGeneratorDemo.sln";
            string csprojFilename = @"C:\Users\User1\source\repos\NugetGeneratorDemo\NugetGeneratorDemo\NugetGeneratorDemo.csproj";

            FileInfo projectFile = new(csprojFilename);
            VSProject vSProject = new("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}", "NugetGeneratorDemo", projectFile);


            vSProject.OnStatusChanged += (vsProject) =>
            {
                if (vsProject.Status == LoadingStatus.Loaded)
                    autoResetEvent.Set();
            };

            if (vSProject.Status != LoadingStatus.Loaded)
            {
                autoResetEvent.WaitOne();
            }

            Assert.IsNotNull(vSProject);
            Assert.AreEqual(LoadingStatus.Loaded, vSProject.Status);

            Assert.IsNotNull(vSProject.References);
            Assert.AreEqual(1, vSProject.References.Count());
            Assert.AreEqual(1, vSProject.ShowWarning().Count());
            Assert.AreEqual("2.0.0", vSProject.Version.ToString());
            foreach (dynamic reference in vSProject.References)
            {
                CheckReference(reference);
            }
        }

        [TestMethod()]
        public void VSNugetDEBUGTest()
        {
            VSSolution vSSolution = InitializeVsSolution();

            Assert.IsNotNull(vSSolution);
            Assert.IsNotNull(vSSolution.SolutionFile);
            Assert.IsTrue(vSSolution.IsProjectsLoaded);
            VSProject? testedProject = vSSolution.Projects.FirstOrDefault(project => project.Name.Equals("NuspecGenerator", StringComparison.Ordinal));
            Assert.IsNotNull(testedProject);
            Assert.IsNotNull(vSSolution.SolutionFile.DirectoryName);

            string outputNugetPath = Path.Combine(vSSolution.SolutionFile.Directory.Parent.FullName, "Packages");
            VisualStudioIDE visualStudioIDE = VSIdeFinder.GetExistingVS().FirstOrDefault();
            Assert.IsNotNull(visualStudioIDE);
            VSNuGet.VSNuGet vSNuget = new VSNuGet.VSNuGet(testedProject, new DirectoryInfo(outputNugetPath));
            Assert.IsNotNull(vSNuget);
            Assert.IsNotNull(vSNuget.Project);
            Assert.AreEqual("NuspecGenerator", vSNuget.Project.Name);
            Assert.AreEqual("NuspecGenerator.Davoud.dll", vSNuget.Project.AssemblyName);
            Assert.IsNotNull(vSNuget.Nuspec);
            Assert.IsTrue(vSNuget.Nuspec.NuSpecFile.Exists);
            Assert.IsTrue(vSNuget.Nuspec.IsReady);
            NuGetVersion versionToGenerate = vSNuget.Prepare("DEBUG");
            visualStudioIDE.Build(vSNuget.Project, "DEBUG");
            vSNuget.Generate(versionToGenerate);
        }

        private static void CheckReference(VSProject vsProject)
        {
            Assert.IsTrue(vsProject.ProjectFile.Exists);
            Assert.AreNotEqual(string.Empty, vsProject.Name);
            Assert.AreNotEqual(string.Empty, vsProject.UniqueId);
            Assert.AreNotEqual(string.Empty, vsProject.Version.ToString());
        }

        private static void CheckReference(VSPackage vsPackage)
        {
            Assert.AreNotEqual(string.Empty, vsPackage.Name);
            Assert.AreNotEqual(string.Empty, vsPackage.Version.ToString());
        }
    }
}