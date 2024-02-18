using EnvDTE;

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

using FoxNuGet.VSNuGet;

using FoxNuGet.VSSolution;

using System;
using System.ComponentModel.Design;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FoxNuGet.VSTools;

using Task = System.Threading.Tasks.Task;
using NuGet.Versioning;

namespace NuGet.Assembly.Versioning.Handler
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class VersioningHandler
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0100;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("e73b3bd6-29fe-48c8-9cab-f01e13036c30");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly AsyncPackage package;

        /// <summary>
        /// Initializes a new instance of the <see cref="VersioningHandler"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        private VersioningHandler(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new OleMenuCommand(this.Execute, menuCommandID, "test");
            //var menuItem = commandService.FindCommand(menuCommandID) as OleMenuCommand;
            //menuItem.
            menuItem.BeforeQueryStatus += MenuItem_BeforeQueryStatus;
            //// Change the button text dynamically
            menuItem.Properties["ButtonText"] = $"Generate1";
            menuItem.Text = $"Generate";

            commandService.AddCommand(menuItem);
        }

        private void MenuItem_BeforeQueryStatus(object sender, EventArgs e)
        {
            // Code to execute before the context menu is displayed
            if (sender is OleMenuCommand menuItem)
            {
                NuGetVersion version = GetNugetVersionAsync().GetAwaiter().GetResult();
                var test = GetDynamicButtonText();
                menuItem.Text = $"version={version}";
            }
        }

        private string GetDynamicButtonText()
        {
            // Retrieve the dynamic text for the button
            // Example: Read from a configuration file
            string dynamicText = System.Configuration.ConfigurationManager.AppSettings["DynamicButtonText"];

            return dynamicText;
        }


        private string GetSelectedProjectPath(DTE dte)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Project project = GetSelectedProject(dte);
            {
                if (project != null)
                {
                    return Path.GetFileName(project.FileName);
                    //Property fullPathProperty = project.Properties.Item("FullPath");
                    //if (fullPathProperty != null)
                    //{
                    //    string projectPath = fullPathProperty.Value.ToString();
                    //    return projectPath;
                    //}
                }
            }
            return null;
        }

        private Project GetSelectedProject(DTE dte)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (dte is null)
            {
                throw new ArgumentNullException(nameof(dte));
            }

            foreach (SelectedItem selectedItem in dte.SelectedItems)
            {
                if (selectedItem.Project != null)
                {
                    return selectedItem.Project;
                }
            }
            return null;
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static VersioningHandler Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private Microsoft.VisualStudio.Shell.IAsyncServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            // Switch to the main thread - the call to AddCommand in VersioningHandler's constructor requires
            // the UI thread.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new VersioningHandler(package, commandService);
        }

        // Handler for the ContextMenuOpening event
        //private void ContextMenuOpeningHandler(object sender, ContextMenuEventArgs e)
        //{
        //    FrameworkElement fe = e.Source as FrameworkElement;
        //    ContextMenu cm = fe.ContextMenu;

        //    // Modify the menu items
        //    foreach (MenuItem mi in cm.Items)
        //    {
        //        if ((string)mi.Header == "Item1")
        //        {
        //            mi.Header = "New Label"; // Change the label of an existing menu item
        //        }
        //        else if ((string)mi.Header == "Item2")
        //        {
        //            cm.Items.Remove(mi); // Remove a menu item
        //        }
        //    }

        //    // Add a new menu item
        //    MenuItem newItem = new MenuItem();
        //    newItem.Header = "Item3";
        //    cm.Items.Add(newItem);
        //}


        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            NuGetVersion version = GetNugetVersionAsync().GetAwaiter().GetResult();

            string message = string.Format(CultureInfo.CurrentCulture, "Inside {0}.MenuItemCallback()", this.GetType().FullName);
            message += $"version={version}";
            string title = "VersioningHandler";

            // Show a message box to prove we were here
            VsShellUtilities.ShowMessageBox(
                this.package,
                message,
                title,
                OLEMSGICON.OLEMSGICON_INFO,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }

        private async Task<NuGetVersion> GetNugetVersionAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (!(await package.GetServiceAsync(typeof(DTE)).ConfigureAwait(false) is DTE dte))
                throw new TypeAccessException(nameof(DTE));


            AutoResetEvent autoResetEvent = new AutoResetEvent(false);
            VSSolution vSSolution = new VSSolution(new FileInfo(dte.Solution.FullName));
            await vSSolution.LoadProjectsAsync().ConfigureAwait(false);
            //vSSolution.OnProjectsLoaded += () =>
            //{
            //    if (vSSolution.IsProjectsLoaded)
            //        autoResetEvent.Set();
            //};
            //autoResetEvent.WaitOne();

            Project SelectedProject = GetSelectedProject(dte);
            VSProject SelectedVsProject = vSSolution.Projects.FirstOrDefault(p => p.ProjectFile.FullName.Equals(SelectedProject.FileName, StringComparison.OrdinalIgnoreCase));
            VisualStudioIDE visualStudioIDE = new VisualStudioIDE(dte.Name, new FileInfo(dte.FullName));
            string outputNugetPath = Path.Combine(vSSolution.SolutionFile.Directory.Parent.FullName, "Packages");
            VSNuGet vSNuget = new VSNuGet(SelectedVsProject, new DirectoryInfo(outputNugetPath), null);

            string currentConfiguration = dte.Solution.SolutionBuild.ActiveConfiguration.Name;
            return vSNuget.GetVersionToGenerate(currentConfiguration);
        }
    }
}
