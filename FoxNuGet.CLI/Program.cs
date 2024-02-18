// TODO: Recenser les NuSpec liées à une solution donnée.
// TODO: Recenser les *.csproj dont au moins un fichier à été modifié sur la branche en cours.
// TODO: Générer la Release Note à partir des commentaires de commits. 
// TODO: Mettre à jour les versions des *.csproj et générer un NuGet
namespace FoxNuGet.CLI
{
    using FoxNuGet.VSSolution;
    using FoxNuGet.VSVersionService;

    using Microsoft.Extensions.Logging;

    using System.IO;
    using System.Linq;

    internal class Program
    {
        private static VSVersionService _vsVersionService;
        static void Main(string[] args)
        {
            FileInfo currentAppFile = new(Environment.ProcessPath);

            DirectoryInfo workingDirectory = new DirectoryInfo(Environment.CurrentDirectory);
#if DEBUG
            if (currentAppFile.Directory.FullName.Equals(workingDirectory.FullName))
            {
                workingDirectory = workingDirectory.Parent.Parent.Parent.Parent;
                //workingDirectory = new DirectoryInfo(@"C:\DassaultDUOS\Git\Bitbucket\fsv3-framework\");
            }
#endif
            _vsVersionService = new(workingDirectory);
            ILogger logger = LoggerInitialization();

            _vsVersionService.Logger = logger;
            _vsVersionService.Refresh();
            ArgumentsManagment(args, currentAppFile);
        }

        private static void ArgumentsManagment(string[] args, FileInfo currentAppFile)
        {
            Console.WriteLine("");

            if (args.Length > 0)
            {
                NuGetGeneration(args);
            }
            else
            {
                SampleDebug();
                Console.WriteLine("Actions possibles : ");
                Console.WriteLine($"{currentAppFile.Name} <nuSpecFileUniqueIdToGenerate> <configurationMode>");
            }
        }

        private static void NuGetGeneration(string[] args)
        {
            string nuSpecFileUniqueIdToGenerate = args[0];
            string configurationMode = (args.Length > 1) ? args[1] : "RELEASE";

            if (!_vsVersionService.VsNuGets.Any(n => n.Nuspec.UniqueId.Equals(nuSpecFileUniqueIdToGenerate, StringComparison.OrdinalIgnoreCase)))
            {
                Console.WriteLine($"nuSpecFileUniqueIdToGenerate \"{nuSpecFileUniqueIdToGenerate}\" not found.");
                Console.WriteLine("Let's see known nuSpecFileUniqueId:");
                int maxLength = _vsVersionService.VsNuGets.Select(n => n.Nuspec.NuSpecFile.Name.Length).OrderByDescending(l => l).FirstOrDefault();
                _vsVersionService.VsNuGets.ForEach(n
                    => Console.WriteLine($"  * \"{n.Nuspec.NuSpecFile.Name.PadRight(maxLength + 2)}\" :  {n.Nuspec.UniqueId}")
                    );
            }

            VSNuGet.VSNuGet vsNuget = _vsVersionService.GetNuGet(nuSpecFileUniqueIdToGenerate);
            if (vsNuget is not null)
            {
                _vsVersionService.GenerateNuGet(vsNuget, configurationMode);
            }
        }

        private static ILogger LoggerInitialization()
        {
            // Create an instance of ILoggerFactory
            ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
            {
                // Add the console logger
                builder.AddSimpleConsole(options =>
                {
                    //options.IncludeScopes = true;
                    options.SingleLine = true;
                    options.TimestampFormat = "HH:mm:ss ";
                });
                builder.AddFilter((provider, category, logLevel) =>
                {
                    if (logLevel >= LogLevel.Information)
                    {
                        // Process log messages with EventId 1000
                        return logLevel >= LogLevel.Information; // && category.Contains("3");
                    }
                    return false;
                });
            });

            // Create a logger using the LoggerFactory
            ILogger logger = loggerFactory.CreateLogger("FoxNuGet");
            return logger;
        }

        private static void SampleDebug()
        {
#if DEBUG
            string targetProject = "FoxNuGet.VSVersionService";
            Console.Write($"Création du NuSpec liée au projet \"{targetProject}\": ");
            VSProject vsProject = _vsVersionService.VsSolutions.SelectMany(s => s.Projects.Where(p => p.Name.Contains(targetProject))).FirstOrDefault();
            VSNuGet.VSNuGet vsNuget = _vsVersionService.GenerateNuSpec(vsProject);
            bool created = vsNuget.Nuspec.NuSpecFile.Exists;
            Console.WriteLine(created ? "Ok" : "Failed");
            Console.WriteLine("");
#endif
        }

        private static void AskAndWait(string value)
        {
            while (value.Split(" ").Length < 2 || value.Equals("-q"))
                value = Console.ReadLine();

            if (value.Equals("-q"))
                return;

            string[] commands = value.Split(" ");
            string nuspecUniqueIdToGenerate;
            string configurationMode = "RELEASE";
            if (commands[0].Equals("-g"))
            {
                nuspecUniqueIdToGenerate = commands[1];
                if (commands.Length > 3)
                    configurationMode = commands[4];
            }
            else
            if (commands.Length > 3 && commands[2].Equals("-g"))
            {
                nuspecUniqueIdToGenerate = commands[3];
                if (commands[0].Equals("-c"))
                    configurationMode = commands[1];
            }
            else
            {
                AskAndWait(Console.ReadLine());
                return;
            }
            VSNuGet.VSNuGet vsNuget = _vsVersionService.GetNuGet(nuspecUniqueIdToGenerate);
            _vsVersionService.GenerateNuGet(vsNuget, configurationMode);
        }
    }
}